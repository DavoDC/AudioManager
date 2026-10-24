"""Launcher verbs for python -m gui.main: --preflight and --stop (WS-003, WS-010, WS-016).

Standard library only, so it can run before the heavy imports and report a readable
failure instead of a silent pythonw crash. Both verbs return an exit code; nothing here
starts the server.
"""
from __future__ import annotations

import importlib.util
import socket
import subprocess
import sys
import time
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path

from gui import applog, config, security

_NO_WINDOW = getattr(subprocess, "CREATE_NO_WINDOW", 0)
REQUIRED_MODULES = ("nicegui", "fastapi", "uvicorn")
MIN_PYTHON = (3, 10)


@dataclass
class Check:
    name: str
    ok: bool
    detail: str


def _check_python() -> Check:
    v = sys.version_info
    return Check("python", v[:2] >= MIN_PYTHON, f"{v.major}.{v.minor}.{v.micro}"
                 + ("" if v[:2] >= MIN_PYTHON else f" (need {MIN_PYTHON[0]}.{MIN_PYTHON[1]}+)"))


def _check_imports() -> Check:
    missing = [m for m in REQUIRED_MODULES if importlib.util.find_spec(m) is None]
    return Check("imports", not missing,
                 "missing: " + ", ".join(missing) + " (pip install -r gui/requirements.txt)"
                 if missing else "all present")


def _check_config() -> Check:
    path = config.TAGFIX_CUSTOM_RULES_XML
    if not path.exists():
        return Check("config", True, f"{path.name} absent (no custom rules)")
    try:
        ET.parse(path)
    except (ET.ParseError, OSError) as e:
        return Check("config", False, f"{path.name} is not readable XML: {e}")
    return Check("config", True, f"{path.name} parses")


def _check_cache_dir() -> Check:
    try:
        config.CACHE_DIR.mkdir(parents=True, exist_ok=True)
        probe = config.CACHE_DIR / ".preflight-write-test"
        probe.write_text("ok", encoding="utf-8")
        probe.unlink()
    except OSError as e:
        return Check("cache dir", False, f"{config.CACHE_DIR} not writable: {e}")
    return Check("cache dir", True, "writable")


def _check_port(port: int) -> Check:
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    try:
        s.bind((security.BIND_HOST, port))
    except OSError:
        return Check("port", False, f"port {port} is in use (python -m gui.main --stop --port {port})")
    finally:
        s.close()
    return Check("port", True, f"{port} is free")


def preflight(port: int) -> list[Check]:
    return [_check_python(), _check_imports(), _check_config(), _check_cache_dir(), _check_port(port)]


def run_preflight(port: int) -> int:
    results = preflight(port)
    for r in results:
        print(f"  {'ok  ' if r.ok else 'FAIL'} {r.name}: {r.detail}")
    bad = [r for r in results if not r.ok]
    print("preflight: OK" if not bad else f"preflight: FAILED ({len(bad)} problem(s))")
    return 0 if not bad else 1


def parse_netstat_listener(text: str, port: int) -> int | None:
    """PID of the process LISTENING on 127.0.0.1:<port> in `netstat -ano` output."""
    for line in text.splitlines():
        parts = line.split()
        if len(parts) >= 5 and parts[0] == "TCP" and parts[3] == "LISTENING" \
                and parts[1].rsplit(":", 1)[-1] == str(port):
            return int(parts[4])
    return None


def find_listener(port: int) -> int | None:
    try:
        out = subprocess.run(["netstat", "-ano", "-p", "TCP"], capture_output=True, text=True,
                             timeout=15, creationflags=_NO_WINDOW).stdout
    except (OSError, subprocess.SubprocessError):
        return None
    return parse_netstat_listener(out, port)


def _alive(pid: int, port: int) -> bool:
    """Still the listener? (netstat, not a connect probe: a full backlog refuses probes.)"""
    return find_listener(port) == pid


def stop(port: int) -> int:
    """Stop whatever listens on the port: polite taskkill first, forced after two seconds."""
    pid = find_listener(port)
    if pid is None:
        print(f"not running on port {port}")
        return 0
    subprocess.run(["taskkill", "/PID", str(pid)], capture_output=True, creationflags=_NO_WINDOW)
    deadline = time.time() + 2
    while time.time() < deadline and _alive(pid, port):
        time.sleep(0.1)
    if _alive(pid, port):
        subprocess.run(["taskkill", "/PID", str(pid), "/F"], capture_output=True,
                       creationflags=_NO_WINDOW)
    applog.write("stop", f"stopped pid {pid} on port {port}")
    print(f"stopped pid {pid} on port {port}")
    return 0


# --- --launch / --status (windowless lifecycle, copied from MediaServer's dashboard launcher) ---

DETACHED_PROCESS = 0x00000008
DROPPED_FLAGS = ("--launch", "--preflight", "--stop", "--status", "--check", "--no-dialog")
LAUNCH_TIMEOUT = 40.0


def pythonw_for(python_exe: str) -> str:
    """pythonw.exe beside python_exe, else python_exe itself (non-Windows run or unusual install)."""
    import os
    folder, name = os.path.split(python_exe)
    candidate = os.path.join(folder, "pythonw.exe")
    return candidate if name.lower().startswith("python") and os.path.exists(candidate) else python_exe


def child_argv(pythonw: str, port: int, passthrough: list[str]) -> list[str]:
    """The server command: the caller's own flags minus the launcher-only ones, plus an explicit --port."""
    rest, skip = [], False
    for i, a in enumerate(passthrough):
        if skip:
            skip = False
        elif a == "--port":
            skip = True
        elif a not in DROPPED_FLAGS and not a.startswith("--port="):
            rest.append(a)
    return [pythonw, "-m", "gui.main", *rest, "--port", str(port)]


def spawn(argv: list[str], cwd: str | None = None, env: dict | None = None, popen=subprocess.Popen):
    """Detached child holding none of the caller's handles (a bare `start` child keeps the caller's pipes open)."""
    import os
    flags = 0 if os.name != "nt" else subprocess.CREATE_NO_WINDOW | subprocess.CREATE_NEW_PROCESS_GROUP | DETACHED_PROCESS
    return popen(argv, cwd=cwd, env=env, stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
                 stderr=subprocess.DEVNULL, close_fds=True, creationflags=flags)


def answers(port: int) -> bool:
    import urllib.request
    try:
        with urllib.request.urlopen(f"http://{security.BIND_HOST}:{port}/api/build", timeout=2) as r:
            return r.status == 200
    except Exception:
        return False


def wait_ready(proc, port: int, timeout: float, ready=answers, sleep=time.sleep, clock=time.monotonic) -> tuple[bool, str]:
    start = clock()
    while True:
        if proc.poll() is not None:
            return False, "the GUI process exited before it answered; see gui/.cache/gui.log and gui/.cache/log/"
        if ready(port):
            return True, ""
        if clock() - start >= timeout:
            return False, f"the GUI did not answer on port {port} within {timeout:.0f} s; see gui/.cache/log/"
        sleep(0.25)


def _say(text: str) -> None:
    import sys
    if sys.stdout is not None:
        print(text)


def _fail(text: str, dialog: bool) -> int:
    """Windowless failure path: log line plus a message box, because pythonw has no console to show it."""
    applog.write("launch", f"FAILED: {text}")
    _say(f"AudioManager GUI not started: {text}")
    if dialog:
        try:
            import ctypes
            ctypes.windll.user32.MessageBoxW(0, text, "AudioManager GUI did not start", 0x10)
        except Exception:
            pass
    return 1


def status(port: int) -> int:
    pid = find_listener(port)
    if pid is None:
        _say(f"not running on port {port}")
        return 1
    _say(f"running pid {pid} on port {port}")
    return 0


def launch(port: int, passthrough: list[str], dialog: bool = True, popen=subprocess.Popen) -> int:
    """Replace any instance on the port, run preflight, start the server detached, return once it answers."""
    import os
    import sys
    env = dict(os.environ)
    if find_listener(port) is not None:
        stop(port)
        env["AM_GUI_NO_BROWSER"] = "1"  # a tab is presumably open already; reload it for the fresh instance
    results = preflight(port)
    bad = [r for r in results if not r.ok]
    if bad:
        return _fail("preflight failed: " + "; ".join(f"{r.name}: {r.detail}" for r in bad), dialog)
    root = str(Path(__file__).resolve().parents[1])
    argv = child_argv(pythonw_for(sys.executable), port, passthrough)
    applog.write("launch", f"spawning {' '.join(argv)}")
    proc = spawn(argv, cwd=root, env=env, popen=popen)
    ok, why = wait_ready(proc, port, LAUNCH_TIMEOUT)
    if not ok:
        return _fail(why, dialog)
    _say(f"AudioManager GUI running on {security.open_url(port)}")
    return 0

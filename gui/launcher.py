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

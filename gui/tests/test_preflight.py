"""--preflight and --stop (WS-003, WS-010, WS-016): readable checks and a scriptable stop."""
import socket
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))

from gui import launcher


def _free_port():
    s = socket.socket()
    s.bind(("127.0.0.1", 0))
    port = s.getsockname()[1]
    s.close()
    return port


def test_preflight_passes_on_a_healthy_checkout():
    results = launcher.preflight(_free_port())
    failed = [r for r in results if not r.ok]
    assert not failed, [f"{r.name}: {r.detail}" for r in failed]
    assert {"python", "imports", "config", "cache dir", "port"} <= {r.name for r in results}


def test_preflight_fails_when_the_port_is_held_by_something_else():
    s = socket.socket()
    s.bind(("127.0.0.1", 0))
    s.listen(1)
    try:
        results = launcher.preflight(s.getsockname()[1])
    finally:
        s.close()
    port = next(r for r in results if r.name == "port")
    assert not port.ok and "in use" in port.detail


def test_preflight_fails_on_a_broken_config_file(tmp_path, monkeypatch):
    bad = tmp_path / "rules.xml"
    bad.write_text("<rules><unclosed></rules>", encoding="utf-8")
    monkeypatch.setattr(launcher.config, "TAGFIX_CUSTOM_RULES_XML", bad)
    cfg = next(r for r in launcher.preflight(_free_port()) if r.name == "config")
    assert not cfg.ok and "rules.xml" in cfg.detail


def test_run_preflight_exit_codes_and_output(capsys):
    assert launcher.run_preflight(_free_port()) == 0
    out = capsys.readouterr().out
    assert "preflight: OK" in out
    s = socket.socket()
    s.bind(("127.0.0.1", 0))
    s.listen(1)
    try:
        code = launcher.run_preflight(s.getsockname()[1])
    finally:
        s.close()
    assert code != 0 and "preflight: FAILED" in capsys.readouterr().out


def test_parse_netstat_finds_the_listener_pid():
    text = ("  TCP    127.0.0.1:8471         0.0.0.0:0              LISTENING       4242\n"
            "  TCP    127.0.0.1:8472         0.0.0.0:0              LISTENING       99\n"
            "  TCP    127.0.0.1:8471         127.0.0.1:50000        ESTABLISHED     4242\n")
    assert launcher.parse_netstat_listener(text, 8471) == 4242
    assert launcher.parse_netstat_listener(text, 9999) is None


def test_stop_with_nothing_running_exits_zero(capsys):
    assert launcher.stop(_free_port()) == 0
    assert "not running" in capsys.readouterr().out


def test_stop_ends_a_real_listener_and_logs(capsys):
    port = _free_port()
    code = ("import socket,time\ns=socket.socket();s.bind(('127.0.0.1',%d));s.listen(1)\n"
            "print('up',flush=True);time.sleep(60)" % port)
    p = subprocess.Popen([sys.executable, "-c", code], stdout=subprocess.PIPE, text=True)
    try:
        assert p.stdout.readline().strip() == "up"
        assert launcher.stop(port) == 0
        for _ in range(40):
            if p.poll() is not None:
                break
            time.sleep(0.1)
        assert p.poll() is not None, "listener still alive after stop"
    finally:
        if p.poll() is None:
            p.kill()
    from gui import applog
    assert any("[stop]" in l for l in applog.tail(20))


def _main(*args):
    return subprocess.run([sys.executable, "-m", "gui.main", *args], cwd=ROOT, capture_output=True,
                          text=True, timeout=60)


def test_cli_preflight_flag_exit_codes():
    ok = _main("--preflight", "--port", str(_free_port()))
    assert ok.returncode == 0 and "preflight: OK" in ok.stdout, ok.stdout + ok.stderr
    s = socket.socket()
    s.bind(("127.0.0.1", 0))
    s.listen(1)
    try:
        bad = _main("--preflight", "--port", str(s.getsockname()[1]))
    finally:
        s.close()
    assert bad.returncode != 0 and "FAILED" in bad.stdout


def test_cli_stop_flag_when_nothing_runs():
    r = _main("--stop", "--port", str(_free_port()))
    assert r.returncode == 0 and "not running" in r.stdout


def test_gui_bat_runs_preflight_before_the_windowless_start():
    bat = (ROOT / "scripts" / "Gui.bat").read_text(encoding="utf-8")
    assert bat.index("gui.main --preflight") < bat.index("pythonw -m gui.main")
    assert "exit /b 0" in bat

"""Autostart at login (WS-012): HKCU Run entry, fully reversible, tested against a fake registry."""
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))

from gui import autostart


class FakeRegistry:
    def __init__(self):
        self.values = {}

    def get(self, name):
        return self.values.get(name)

    def set(self, name, value):
        self.values[name] = value

    def delete(self, name):
        self.values.pop(name, None)


def test_command_uses_pythonw_the_main_file_and_no_browser():
    cmd = autostart.command(python_exe=r"C:\Python\python.exe")
    assert cmd.startswith('"C:\Python\pythonw.exe"')
    assert "gui" in cmd and "main.py" in cmd and cmd.endswith("--no-browser")


def test_command_falls_back_to_given_exe_when_already_pythonw():
    assert autostart.command(python_exe=r"C:\P\pythonw.exe").startswith('"C:\P\pythonw.exe"')


def test_enable_then_status_then_disable():
    reg = FakeRegistry()
    assert autostart.status(reg) is False
    autostart.enable(reg)
    assert autostart.status(reg) is True
    assert reg.values[autostart.VALUE_NAME] == autostart.command()
    autostart.disable(reg)
    assert autostart.status(reg) is False and reg.values == {}


def test_disable_when_absent_is_a_no_op():
    reg = FakeRegistry()
    autostart.disable(reg)
    assert reg.values == {}


def test_enable_twice_keeps_one_entry():
    reg = FakeRegistry()
    autostart.enable(reg)
    autostart.enable(reg)
    assert list(reg.values) == [autostart.VALUE_NAME]


def test_status_is_false_when_entry_points_elsewhere():
    reg = FakeRegistry()
    reg.set(autostart.VALUE_NAME, r'"C:\Old\pythonw.exe" "C:\Old\gui\main.py"')
    assert autostart.status(reg) is False  # stale path: not ours, needs `on` to refresh


def test_run_verb_prints_and_returns_codes(capsys):
    reg = FakeRegistry()
    assert autostart.run("status", reg) == 0 and "off" in capsys.readouterr().out
    assert autostart.run("on", reg) == 0 and "on" in capsys.readouterr().out
    assert autostart.run("status", reg) == 0 and "on" in capsys.readouterr().out
    assert autostart.run("off", reg) == 0
    assert autostart.run("bogus", reg) == 2


def test_cli_status_is_read_only_and_exits_zero():
    r = subprocess.run([sys.executable, "-m", "gui.main", "--autostart", "status"], cwd=ROOT,
                       capture_output=True, text=True, timeout=60, creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
    assert r.returncode == 0 and "autostart:" in r.stdout, r.stdout + r.stderr

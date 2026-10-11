"""Start the GUI at login (WS-012): one HKCU Run value, no admin rights, fully reversible.

python -m gui.main --autostart on|off|status. The value launches pythonw (windowless) on
gui/main.py with --no-browser, so login does not pop a tab; the tray icon is the way in.
The registry is injected so tests never touch the real one.
"""
from __future__ import annotations

import sys
from pathlib import Path

VALUE_NAME = "AudioManagerGui"
RUN_KEY = r"Software\Microsoft\Windows\CurrentVersion\Run"
MAIN_PY = Path(__file__).resolve().parent / "main.py"


def command(python_exe: str | None = None) -> str:
    exe = Path(python_exe or sys.executable)
    pyw = exe if exe.name.lower() == "pythonw.exe" else exe.with_name("pythonw.exe")
    return f'"{pyw}" "{MAIN_PY}" --no-browser'


class WinRegistry:
    """HKCU Run key via winreg."""

    def get(self, name: str) -> str | None:
        import winreg
        try:
            with winreg.OpenKey(winreg.HKEY_CURRENT_USER, RUN_KEY) as k:
                return winreg.QueryValueEx(k, name)[0]
        except FileNotFoundError:
            return None

    def set(self, name: str, value: str) -> None:
        import winreg
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, RUN_KEY, 0, winreg.KEY_SET_VALUE) as k:
            winreg.SetValueEx(k, name, 0, winreg.REG_SZ, value)

    def delete(self, name: str) -> None:
        import winreg
        try:
            with winreg.OpenKey(winreg.HKEY_CURRENT_USER, RUN_KEY, 0, winreg.KEY_SET_VALUE) as k:
                winreg.DeleteValue(k, name)
        except FileNotFoundError:
            pass


def status(reg) -> bool:
    """On only when the stored command is exactly what we would write now."""
    return reg.get(VALUE_NAME) == command()


def enable(reg) -> None:
    reg.set(VALUE_NAME, command())


def disable(reg) -> None:
    reg.delete(VALUE_NAME)


def run(verb: str, reg=None) -> int:
    reg = reg or WinRegistry()
    if verb == "on":
        enable(reg)
    elif verb == "off":
        disable(reg)
    elif verb != "status":
        print(f"unknown autostart verb {verb!r}: use on, off or status")
        return 2
    print("autostart: " + ("on" if status(reg) else "off"))
    return 0

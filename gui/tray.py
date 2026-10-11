"""Tray icon hosted in the GUI process (WS-011, WS-014).

Colour follows gui.health (green, amber, red) through gui.icon, the same renderer the
favicon uses. A named Windows mutex keeps one icon per login session: a hot reload starts
the new process before the old one has exited, so the new process retries the lock for a
few seconds. Menu: Open, Reload, Stop GUI. Disable with AM_GUI_TRAY=0; fixture runs
(demo data, used by the capture harness) never get a tray.
"""
from __future__ import annotations

import os
import sys
import threading
import time
import webbrowser
from typing import Callable

from gui import applog, health, icon, security

LOCK_NAME = "Local" + chr(92) + "AudioManagerGuiTray"  # Local\AudioManagerGuiTray
LOCK_RETRY_SECONDS = 8.0
POLL_SECONDS = 5.0
TITLE_MAX = 127  # Windows tooltip limit
ERROR_ALREADY_EXISTS = 183


def enabled(fixture: str | None = None) -> bool:
    return os.environ.get("AM_GUI_TRAY", "1") != "0" and not fixture


def acquire_lock(name: str = LOCK_NAME):
    """Named mutex handle, or None when another live process already holds it."""
    if sys.platform != "win32":
        return object()
    import ctypes
    kernel32 = ctypes.windll.kernel32
    kernel32.CreateMutexW.restype = ctypes.c_void_p
    handle = kernel32.CreateMutexW(None, False, name)
    if not handle:
        return None
    if ctypes.get_last_error() == ERROR_ALREADY_EXISTS or kernel32.GetLastError() == ERROR_ALREADY_EXISTS:
        kernel32.CloseHandle(ctypes.c_void_p(handle))
        return None
    return handle


def release_lock(handle) -> None:
    if handle is None or sys.platform != "win32":
        return
    import ctypes
    ctypes.windll.kernel32.CloseHandle(ctypes.c_void_p(handle))


class TrayController:
    """Pushes the current verdict into a pystray-like icon object (icon, title attributes)."""

    def __init__(self, tray_icon, compute: Callable[[], dict] = health.compute):
        self.tray_icon = tray_icon
        self.compute = compute
        self._last: tuple | None = None

    def update(self) -> None:
        v = self.compute()
        key = (v["verdict"], tuple(v["reasons"]))
        if key == self._last:
            return
        self._last = key
        title = "AudioManager: " + v["word"] + (" - " + "; ".join(v["reasons"]) if v["reasons"] else "")
        self.tray_icon.icon = icon.render(v["verdict"], 64)
        self.tray_icon.title = title[:TITLE_MAX]


def request_reload() -> None:
    from gui import hot_reload
    os.makedirs(os.path.dirname(hot_reload.TRIGGER_PATH), exist_ok=True)
    with open(hot_reload.TRIGGER_PATH, "a", encoding="utf-8"):
        os.utime(hot_reload.TRIGGER_PATH, None)


def menu_items(port: int, stop_cb: Callable[[], None]) -> list[tuple[str, Callable[[], None]]]:
    return [("Open", lambda: webbrowser.open(security.open_url(port))),
            ("Reload", request_reload),
            ("Stop GUI", stop_cb)]


def menu_action(cb: Callable[[], None]):
    """pystray inspects the callable's arity: it must take exactly (icon, item)."""
    return lambda _icon, _item: cb()


def start(port: int, stop_cb: Callable[[], None] | None = None, fixture: str | None = None):
    """Start the tray on a daemon thread. Returns the thread, or None when disabled."""
    if not enabled(fixture):
        return None
    t = threading.Thread(target=_run, args=(port, stop_cb or (lambda: os._exit(0))),
                         name="am-tray", daemon=True)
    t.start()
    return t


def _run(port: int, stop_cb: Callable[[], None]) -> None:
    try:
        import pystray
    except ImportError:
        applog.write("tray", "pystray not installed; no tray icon")
        return
    deadline = time.time() + LOCK_RETRY_SECONDS
    lock = acquire_lock()
    while lock is None and time.time() < deadline:
        time.sleep(0.5)
        lock = acquire_lock()
    if lock is None:
        applog.write("tray", "another GUI process owns the tray icon; skipping")
        return
    try:
        items = [pystray.MenuItem(label, menu_action(cb), default=(label == "Open"))
                 for label, cb in menu_items(port, stop_cb)]
        ti = pystray.Icon("AudioManager", icon.render("amber", 64), "AudioManager", pystray.Menu(*items))
        ctl = TrayController(ti)

        def poll() -> None:
            while True:
                try:
                    ctl.update()
                except Exception as e:  # the tray must never take the app down
                    applog.write("tray", f"update failed: {e}")
                time.sleep(POLL_SECONDS)

        threading.Thread(target=poll, name="am-tray-poll", daemon=True).start()
        applog.write("tray", "icon started")
        ti.run()
    finally:
        release_lock(lock)

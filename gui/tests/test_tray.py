"""Tray icon (WS-011): one icon per machine session, colour follows the verdict, safe to disable."""
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))

import pytest

from gui import icon, tray


class FakeIcon:
    def __init__(self):
        self.icon = None
        self.title = ""
        self.stopped = False

    def stop(self):
        self.stopped = True


def test_disabled_by_env_var(monkeypatch):
    monkeypatch.setenv("AM_GUI_TRAY", "0")
    assert tray.enabled() is False
    monkeypatch.delenv("AM_GUI_TRAY")
    assert tray.enabled() is True


def test_fixture_runs_do_not_get_a_tray(monkeypatch):
    monkeypatch.delenv("AM_GUI_TRAY", raising=False)
    assert tray.enabled(fixture="normal") is False


@pytest.mark.skipif(sys.platform != "win32", reason="named mutex is Windows only")
def test_second_lock_with_the_same_name_is_refused():
    name = tray.LOCK_NAME + "Test"
    first = tray.acquire_lock(name)
    try:
        assert first is not None
        assert tray.acquire_lock(name) is None
    finally:
        tray.release_lock(first)
    again = tray.acquire_lock(name)
    try:
        assert again is not None  # released, so a reload can take it
    finally:
        tray.release_lock(again)


def test_update_sets_the_icon_to_the_verdict_colour_and_title():
    fake = FakeIcon()
    seq = iter([{"verdict": "green", "word": "Healthy", "reasons": []},
                {"verdict": "red", "word": "Broken", "reasons": ["AudioManager.exe not found"]}])
    ctl = tray.TrayController(fake, lambda: next(seq))
    ctl.update()
    assert fake.icon.getpixel((32, 32))[:3] == icon.render("green", 64).getpixel((32, 32))[:3]
    assert "Healthy" in fake.title
    ctl.update()
    assert fake.icon.getpixel((32, 32))[:3] == icon.render("red", 64).getpixel((32, 32))[:3]
    assert "exe not found" in fake.title


def test_update_skips_redraw_when_nothing_changed():
    fake = FakeIcon()
    ctl = tray.TrayController(fake, lambda: {"verdict": "green", "word": "Healthy", "reasons": []})
    ctl.update()
    first = fake.icon
    ctl.update()
    assert fake.icon is first


def test_title_is_short_enough_for_windows():
    fake = FakeIcon()
    ctl = tray.TrayController(fake, lambda: {"verdict": "amber", "word": "Attention", "reasons": ["x" * 400]})
    ctl.update()
    assert len(fake.title) <= 127


def test_menu_has_open_reload_stop():
    labels = [label for label, _ in tray.menu_items(8471, stop_cb=lambda: None)]
    assert labels == ["Open", "Reload", "Stop GUI"]


def test_reload_item_touches_the_trigger(tmp_path, monkeypatch):
    from gui import hot_reload
    trig = tmp_path / "reload.trigger"
    monkeypatch.setattr(hot_reload, "TRIGGER_PATH", str(trig))
    tray.request_reload()
    assert trig.exists()


def test_start_returns_none_when_disabled(monkeypatch):
    monkeypatch.setenv("AM_GUI_TRAY", "0")
    assert tray.start(8471) is None


def test_menu_actions_are_accepted_by_pystray():
    pystray = pytest.importorskip("pystray")
    called = []
    for label, cb in tray.menu_items(8471, stop_cb=lambda: called.append("stop")):
        item = pystray.MenuItem(label, tray.menu_action(cb))  # raised ValueError on a bad arity
        assert item.text == label
    tray.menu_action(lambda: called.append("x"))(None, None)
    assert called == ["x"]

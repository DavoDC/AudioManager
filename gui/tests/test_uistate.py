"""State contract: every tab exposes JSON with row test ids the DOM also paints."""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

import pytest

from gui import uistate


def test_testid_is_slug_and_deterministic():
    a = uistate.testid("row-lib", r"\Artists\X\a.xml")
    assert a == uistate.testid("row-lib", r"\Artists\X\a.xml")
    assert a.startswith("row-lib-")
    assert all(c.isalnum() or c in "-_" for c in a)


def test_testid_distinguishes_colliding_slugs():
    assert uistate.testid("r", "a b") != uistate.testid("r", "a-b")


def test_tabs_list_matches_main():
    from gui import tabdefs
    assert [k for k, _ in uistate.TAB_LABELS] == [k for k, _l, _b in tabdefs.TABS]


@pytest.mark.parametrize("tab", [k for k, _ in uistate.TAB_LABELS])
def test_every_tab_payload_has_common_fields(tab):
    p = uistate.tab_state(tab)
    assert p["schema"] == uistate.SCHEMA
    assert p["tab"] == tab
    assert isinstance(p["rows"], list)
    assert "generated_at" in p and "stale" in p and "label" in p
    ids = [r["testid"] for r in p["rows"]]
    assert len(ids) == len(set(ids)), "row test ids must be unique"


def test_unknown_tab_returns_none():
    assert uistate.tab_state("nope") is None


def test_full_state_has_all_tabs_and_busy():
    p = uistate.full_state()
    assert set(p["tabs"]) == {k for k, _ in uistate.TAB_LABELS}
    assert "busy" in p and "build" in p and "fixture" in p


def test_integration_rows_follow_entries():
    from gui.tabs.integration import S
    old = S.entries
    try:
        S.entries = [{"filename": "a b.mp3", "title": "T", "artist": "A", "album": "", "destination": "X",
                      "status": "ok", "isNewFolder": False, "inBatchDuplicate": False,
                      "libraryDuplicate": False}]
        S.decisions = {}
        rows = uistate.tab_state("integration")["rows"]
        assert len(rows) == 1
        assert rows[0]["testid"] == uistate.testid("row-int", "a b.mp3")
        assert rows[0]["accepted"] is True
    finally:
        S.entries = old


def test_acquire_rows_follow_state():
    from gui.tabs.acquire import _state, _row_keys_for_tracks
    saved = dict(_state)
    try:
        tracks = [("A", "T", "Al", "2020", "3:00", "http://x")]
        _state["tracks"] = tracks
        _state["downloaded"] = {}
        rows = uistate.tab_state("acquire")["rows"]
        keys = _row_keys_for_tracks(tracks)
        assert rows[0]["testid"] == uistate.testid("row-acq", keys[0])
    finally:
        _state.clear()
        _state.update(saved)

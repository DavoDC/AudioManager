"""Library Browser track detail panel (GUI-2): full tags + file path, exposed as a data-testid."""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from gui import uistate
from gui.data_loader import TrackIndex
from gui.state import state
from gui.tabs import library

TRACK = {
    "id": "\\Artists\\Known\\Singles\\Known - Song.xml", "title": "Song", "artists": "Known; Other",
    "primaryArtist": "Known", "album": "Alb", "year": "2020", "genres": "Hip-Hop; Rap",
    "primaryGenre": "Hip-Hop", "trackNumber": "3", "length": "3:33", "compilation": False,
    "coverWidth": 500, "coverHeight": 500, "hasArt": True, "hiResArt": False,
    "addedDate": "2026-07-01", "filePath": "C:\\Audio\\Artists\\Known\\Singles\\Known - Song.mp3",
}


def test_detail_fields_show_full_tags_and_path():
    got = dict(library.detail_fields(TRACK))
    assert got["Title"] == "Song"
    assert got["Artists"] == "Known; Other"
    assert got["Genres"] == "Hip-Hop; Rap"
    assert got["Track number"] == "3"
    assert got["Compilation"] == "No"
    assert got["Cover"] == "500x500 (below 800px)"
    assert got["File path"] == TRACK["filePath"]


def test_detail_fields_missing_values_show_dash():
    got = dict(library.detail_fields({"id": "x"}))
    assert got["Title"] == "-"
    assert got["File path"] == "-"
    assert got["Cover"] == "No cover"


def test_library_state_reports_selected_track(monkeypatch):
    monkeypatch.setattr(state, "tracks", TrackIndex({"tracks": [TRACK]}))
    monkeypatch.setattr(library.L, "selected", TRACK["id"])
    p = uistate.tab_state("library")
    assert p["selected"] == TRACK["id"]
    assert p["detail_testid"] == "lib-detail"


def test_library_state_nothing_selected_by_default(monkeypatch):
    monkeypatch.setattr(state, "tracks", TrackIndex({"tracks": [TRACK]}))
    monkeypatch.setattr(library.L, "selected", None)
    p = uistate.tab_state("library")
    assert p["selected"] is None

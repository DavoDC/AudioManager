"""Unit tests for gui.tabs.acquire.match_downloads (pure, no filesystem writes)."""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
sys.path.insert(0, str(Path(__file__).resolve().parents[2].parent / "SpotifyTools"))

from gui import config
from gui.tabs.acquire import (
    _length_to_seconds,
    _load_history,
    _save_last_playlist,
    _sorted_tracks,
    _state,
    find_extra_newmusic_files,
    match_downloads,
)


def test_matches_downloaded_file(tmp_path):
    (tmp_path / "Eminem - Lose Yourself.mp3").write_bytes(b"")
    found, missing = match_downloads([("Eminem", "Lose Yourself")], tmp_path)
    assert found == ["Eminem - Lose Yourself"]
    assert missing == []


def test_reports_missing_file(tmp_path):
    found, missing = match_downloads([("Eminem", "Lose Yourself")], tmp_path)
    assert found == []
    assert missing == ["Eminem - Lose Yourself"]


def test_ignores_non_mp3_files(tmp_path):
    (tmp_path / "Eminem - Lose Yourself.txt").write_bytes(b"")
    found, missing = match_downloads([("Eminem", "Lose Yourself")], tmp_path)
    assert missing == ["Eminem - Lose Yourself"]


def test_nonexistent_dir_reports_all_missing(tmp_path):
    found, missing = match_downloads([("Eminem", "Lose Yourself")], tmp_path / "does_not_exist")
    assert found == []
    assert len(missing) == 1


def test_shared_artist_does_not_false_positive_across_titles(tmp_path):
    """Regression: combined artist+title word-set overlap let a shared artist
    name swamp the ratio - "Jack Harlow" (2 words) + a short title tipped the
    overlap over 0.5 against any other Jack Harlow file, marking whole
    catalogues downloaded off one real file. Titles must match independently."""
    (tmp_path / "Jack Harlow - Lonesome.mp3").write_bytes(b"")
    found, missing = match_downloads(
        [("Jack Harlow", "Lonesome"), ("Jack Harlow", "Prague"), ("Jack Harlow", "My Winter")],
        tmp_path,
    )
    assert found == ["Jack Harlow - Lonesome"]
    assert missing == ["Jack Harlow - Prague", "Jack Harlow - My Winter"]


def test_extra_finds_file_not_in_playlist(tmp_path):
    (tmp_path / "Eminem - Lose Yourself.mp3").write_bytes(b"")
    (tmp_path / "Drake - Hotline Bling.mp3").write_bytes(b"")
    extra = find_extra_newmusic_files([("Eminem", "Lose Yourself")], tmp_path)
    assert extra == [("Drake", "Hotline Bling")]


def test_extra_empty_when_all_files_match_playlist(tmp_path):
    (tmp_path / "Eminem - Lose Yourself.mp3").write_bytes(b"")
    extra = find_extra_newmusic_files([("Eminem", "Lose Yourself")], tmp_path)
    assert extra == []


def test_extra_returns_every_file_when_no_playlist_loaded(tmp_path):
    (tmp_path / "Eminem - Lose Yourself.mp3").write_bytes(b"")
    extra = find_extra_newmusic_files([], tmp_path)
    assert extra == [("Eminem", "Lose Yourself")]


# ------------------------------------------------------- history persistence


def test_save_last_playlist_writes_id_and_history(tmp_path, monkeypatch):
    monkeypatch.setattr(config, "CACHE_DIR", tmp_path)
    monkeypatch.setattr(config, "ACQUIRE_STATE_JSON", tmp_path / "acquire-state.json")
    _save_last_playlist("pl1", "First Playlist")
    history = _load_history()
    assert history == [{"id": "pl1", "name": "First Playlist"}]


def test_save_last_playlist_dedupes_by_id_moving_to_front(tmp_path, monkeypatch):
    monkeypatch.setattr(config, "CACHE_DIR", tmp_path)
    monkeypatch.setattr(config, "ACQUIRE_STATE_JSON", tmp_path / "acquire-state.json")
    _save_last_playlist("pl1", "First")
    _save_last_playlist("pl2", "Second")
    _save_last_playlist("pl1", "First Renamed")
    history = _load_history()
    assert [h["id"] for h in history] == ["pl1", "pl2"]
    assert history[0]["name"] == "First Renamed"


def test_save_last_playlist_caps_history_at_five(tmp_path, monkeypatch):
    monkeypatch.setattr(config, "CACHE_DIR", tmp_path)
    monkeypatch.setattr(config, "ACQUIRE_STATE_JSON", tmp_path / "acquire-state.json")
    for i in range(7):
        _save_last_playlist(f"pl{i}", f"Playlist {i}")
    history = _load_history()
    assert len(history) == 5
    assert [h["id"] for h in history] == ["pl6", "pl5", "pl4", "pl3", "pl2"]


# ------------------------------------------------------------------- sorting


def test_length_to_seconds_parses_mm_ss():
    assert _length_to_seconds("3:45") == 225


def test_length_to_seconds_returns_zero_on_malformed_input():
    assert _length_to_seconds("not-a-length") == 0


def test_sorted_tracks_unsorted_preserves_original_order_and_indices():
    _state["tracks"] = [("B Artist", "T1", "", "", "1:00", ""), ("A Artist", "T2", "", "", "2:00", "")]
    _state["sort_col"] = None
    assert _sorted_tracks() == [(0, _state["tracks"][0]), (1, _state["tracks"][1])]


def test_sorted_tracks_by_artist_ascending():
    _state["tracks"] = [("B Artist", "T1", "", "", "1:00", ""), ("A Artist", "T2", "", "", "2:00", "")]
    _state["sort_col"] = "Artist"
    _state["sort_reverse"] = False
    result = _sorted_tracks()
    assert [row[1][0] for row in result] == ["A Artist", "B Artist"]
    assert [row[0] for row in result] == [1, 0]  # original indices preserved through the sort


def test_sorted_tracks_year_blanks_sort_last():
    _state["tracks"] = [("A", "T1", "", "", "1:00", ""), ("B", "T2", "2020", "", "1:00", "")]
    _state["tracks"][0] = ("A", "T1", "Alb", "", "1:00", "")
    _state["tracks"][1] = ("B", "T2", "Alb", "2020", "1:00", "")
    _state["sort_col"] = "Year"
    _state["sort_reverse"] = False
    result = _sorted_tracks()
    assert [row[1][3] for row in result] == ["2020", ""]


def test_matches_when_fetched_track_has_extra_collab_artists(tmp_path):
    """Regression: a Spotify track's artist field can carry featured/collab
    artists ("DC The Don & Someone") that the downloaded filename never
    includes, so exact-artist equality missed a real match. Primary artist
    (text before " & ") plus a case-insensitive substring check now covers it."""
    (tmp_path / "DC THE DON - Yellow.mp3").write_bytes(b"")
    found, missing = match_downloads([("DC The Don & Someone", "Yellow")], tmp_path)
    assert found == ["DC The Don & Someone - Yellow"]
    assert missing == []

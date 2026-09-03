"""Unit tests for gui.tabs.acquire.match_downloads (pure, no filesystem writes)."""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
sys.path.insert(0, str(Path(__file__).resolve().parents[2].parent / "SpotifyTools" / "src"))

from gui import config
from gui.tabs.acquire import (
    _extra_batch_header,
    _extra_segment_label,
    _length_to_seconds,
    _load_history,
    _poll_should_skip,
    _read_mp3_tags,
    _sample_extra,
    _sample_tracks,
    _save_last_playlist,
    _sorted_tracks,
    _spotify_client,
    _state,
    find_extra_newmusic_files,
    match_downloads,
    simulate,
)

import spotify_tools.config as spotify_config
import spotify_tools.spotify_client as spotify_client_module


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
    assert extra == [("Drake", "Hotline Bling", tmp_path / "Drake - Hotline Bling.mp3")]


def test_extra_empty_when_all_files_match_playlist(tmp_path):
    (tmp_path / "Eminem - Lose Yourself.mp3").write_bytes(b"")
    extra = find_extra_newmusic_files([("Eminem", "Lose Yourself")], tmp_path)
    assert extra == []


def test_extra_returns_every_file_when_no_playlist_loaded(tmp_path):
    (tmp_path / "Eminem - Lose Yourself.mp3").write_bytes(b"")
    extra = find_extra_newmusic_files([], tmp_path)
    assert extra == [("Eminem", "Lose Yourself", tmp_path / "Eminem - Lose Yourself.mp3")]


# --------------------------------------------------------------- tag reading


def test_read_mp3_tags_nonexistent_file_returns_blanks(tmp_path):
    album, year, length = _read_mp3_tags(tmp_path / "does_not_exist.mp3")
    assert (album, year, length) == ("", "", "")


def test_read_mp3_tags_corrupt_file_returns_blanks(tmp_path):
    """A file that exists but isn't a valid mp3 (e.g. an empty placeholder
    written in a test fixture) must never raise - both mutagen calls are
    wrapped independently so a bad file degrades to blanks, never crashes
    the row render."""
    bad = tmp_path / "Artist - Title.mp3"
    bad.write_bytes(b"not a real mp3 file")
    album, year, length = _read_mp3_tags(bad)
    assert (album, year, length) == ("", "", "")


def test_read_mp3_tags_reads_real_newmusic_file_if_any_exist():
    """Integration check against the real NEWMUSIC_DIR (read-only) - skipped
    if the folder isn't present/empty on this machine (e.g. CI). Confirms
    _read_mp3_tags doesn't blow up on real files and Length, when present,
    matches the "M:SS" format used elsewhere in this file."""
    if not config.NEWMUSIC_DIR.is_dir():
        return
    mp3_files = list(config.NEWMUSIC_DIR.glob("*.mp3"))
    if not mp3_files:
        return
    album, year, length = _read_mp3_tags(mp3_files[0])
    assert isinstance(album, str) and isinstance(year, str) and isinstance(length, str)
    if length:
        assert re.match(r"^\d+:\d{2}$", length)


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


def test_extra_segment_label_uses_browse_wording_with_no_playlist_loaded():
    """Regression: with no playlist ever fetched, find_extra_newmusic_files()
    correctly returns every NewMusic file, but labelling that "extra" reads
    as an anomaly - see IDEAS.md "Acquire tab incident, 2026-09-04" P1."""
    assert _extra_segment_label(25, playlist_loaded=False) == "25 files in NewMusic"


def test_extra_segment_label_uses_diff_wording_once_playlist_loaded():
    assert _extra_segment_label(3, playlist_loaded=True) == "3 extra in NewMusic"


def test_extra_batch_header_uses_browse_wording_with_no_playlist_loaded():
    assert _extra_batch_header(25, playlist_loaded=False) == "NEWMUSIC FOLDER (25)"


def test_extra_batch_header_uses_diff_wording_once_playlist_loaded():
    assert _extra_batch_header(3, playlist_loaded=True) == "IN NEWMUSIC, NOT IN THIS PLAYLIST (3)"


# --------------------------------------------------------- _spotify_client()
#
# Regression coverage for the Acquire tab incident, 2026-09-04 (see IDEAS.md):
# SpotifyTools' CONFIG_PATH silently pointed at a nonexistent path after a
# refactor, and _spotify_client() - the function that actually loads that
# config and constructs the real Spotify client - was never exercised by any
# test, so a fully green AudioManager suite shipped a broken fetch path.
# These tests cover both branches of _spotify_client()'s own logic (config
# missing vs. config present) without touching the network or a real
# config.json.


def test_spotify_client_raises_when_config_not_found(monkeypatch):
    monkeypatch.setattr(spotify_config, "load_config", lambda path: None)
    try:
        _spotify_client()
        assert False, "_spotify_client() should have raised RuntimeError"
    except RuntimeError as exc:
        assert "SpotifyTools config not found at" in str(exc)
        assert str(spotify_config.CONFIG_PATH) in str(exc)


def test_sample_tracks_returns_expected_shape():
    """Must match the (artist, title, album, year, length, url) 6-tuple
    _do_fetch_tracks() returns, since simulate() drops these straight into
    _state["tracks"] and track_table() unpacks that exact shape."""
    tracks = _sample_tracks()
    assert len(tracks) >= 5
    for row in tracks:
        assert len(row) == 6
        artist, title, album, year, length, url = row
        assert artist and title and url


def test_sample_extra_returns_expected_shape():
    """Must match the (artist, title, url, path) 4-tuple _state["extra"]
    holds (built in _run_check_against_downloads), not the 3-tuple
    find_extra_newmusic_files() returns - track_table() unpacks 4 values."""
    extra = _sample_extra()
    assert len(extra) >= 3
    for row in extra:
        assert len(row) == 4
        artist, title, url, path = row
        assert artist and title and url
        assert isinstance(path, Path)


def test_sample_extra_paths_need_not_exist_and_read_mp3_tags_degrades_cleanly():
    """The synthetic extra paths are fabricated, not real files - confirms
    _read_mp3_tags() (already covered above for a missing file) is safe to
    call on every one of them, exactly as track_table() does at render time."""
    for _artist, _title, _url, path in _sample_extra():
        assert not path.exists()
        album, year, length = _read_mp3_tags(path)
        assert (album, year, length) == ("", "", "")


# --------------------------------------------------------------- simulate()


def test_simulate_sets_simulated_and_playlist_loaded():
    simulate()
    assert _state["simulated"] is True
    assert _state["playlist_loaded"] is True


def test_simulate_populates_tracks_and_extra():
    simulate()
    assert _state["tracks"]
    assert _state["extra"]


def test_simulate_produces_both_downloaded_and_missing_tracks():
    """Regression coverage for the spec: one click must exercise every
    row-state branch in track_table() - some downloaded, some missing."""
    simulate()
    values = list(_state["downloaded"].values())
    assert any(values)
    assert not all(values)


def test_poll_should_skip_true_while_simulated():
    """Regression: _poll_downloads()'s 2s timer used to recompute
    _state["downloaded"]/["extra"] from the real NEWMUSIC_DIR unconditionally,
    clobbering simulate()'s sample data within a couple of poll cycles - see
    IDEAS.md "Acquire tab Simulate-mode exploration, 2026-09-04". Confirms the
    guard the poll checks before touching _state at all."""
    simulate()
    assert _poll_should_skip() is True


def test_poll_should_skip_false_after_clear():
    simulate()
    assert _poll_should_skip() is True
    _state["simulated"] = False
    assert _poll_should_skip() is False


def test_simulate_then_poll_should_skip_leaves_sample_data_untouched():
    """Confirms the guard is load-bearing: simulate()'s sample downloaded/extra
    data is still exactly what simulate() set once _poll_should_skip() has
    been checked (nothing in the guard check itself mutates _state), matching
    what _poll_downloads()'s early return guarantees against a real poll tick."""
    simulate()
    sample_downloaded = dict(_state["downloaded"])
    sample_extra = list(_state["extra"])
    assert _poll_should_skip() is True
    assert _state["downloaded"] == sample_downloaded
    assert _state["extra"] == sample_extra


def test_spotify_client_constructs_real_client_when_config_present(monkeypatch):
    fake_cfg = {"client_id": "id", "client_secret": "secret", "redirect_uri": "http://localhost"}
    monkeypatch.setattr(spotify_config, "load_config", lambda path: fake_cfg)

    calls = []

    class FakeRealSpotifyClient:
        def __init__(self, cfg):
            calls.append(cfg)
            self.cfg = cfg

    monkeypatch.setattr(spotify_client_module, "RealSpotifyClient", FakeRealSpotifyClient)

    client = _spotify_client()

    assert isinstance(client, FakeRealSpotifyClient)
    assert client.cfg == fake_cfg
    assert calls == [fake_cfg]

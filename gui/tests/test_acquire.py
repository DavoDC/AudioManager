"""Unit tests for gui.tabs.acquire.match_downloads (pure, no filesystem writes)."""
import asyncio
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
sys.path.insert(0, str(Path(__file__).resolve().parents[2].parent / "SpotifyTools" / "src"))

from gui import config
import gui.tabs.acquire as acquire_module
from gui.tabs.acquire import (
    _DEEMIX_LINK_LABEL,
    _LIKED_SONGS_ID,
    _LIKED_SONGS_NAME,
    _do_fetch_liked_tracks,
    _do_fetch_tracks,
    _do_sync_liked,
    _downloaded_cell_text,
    _extra_batch_header,
    _extra_segment_label,
    _format_duration,
    _header_label,
    _length_to_seconds,
    _load_history,
    _load_last_playlist_id,
    _load_manual_overrides,
    _load_tracks_cache,
    _poll_should_skip,
    _read_mp3_tags,
    _run_check_against_downloads,
    _run_sync_liked,
    _sample_extra,
    _sample_tracks,
    _save_last_playlist,
    _save_tracks_cache,
    _segment_shows_label,
    _sorted_tracks,
    _spotify_client,
    _state,
    build_track_rows,
    clear_tab_state,
    find_extra_newmusic_files,
    match_downloads,
    progress_metrics,
    restore_cached_tracks,
    simulate,
    toggle_manual_override,
)

import spotify_tools.config as spotify_config
import spotify_tools.spotify_client as spotify_client_module
from spotify_tools.spotify_simulator import SimulatedSpotifyClient


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


def test_matches_downloaded_file_in_subfolder(tmp_path):
    """Regression: Deemix commonly writes an album into its own subfolder
    (e.g. NewMusic/Some Album/Artist - Title.mp3), and the C# side always
    scans with SearchOption.AllDirectories. The GUI's scan must match that
    behavior, not just the top level of NEWMUSIC_DIR."""
    album_dir = tmp_path / "Some Album"
    album_dir.mkdir()
    (album_dir / "Eminem - Lose Yourself.mp3").write_bytes(b"")
    found, missing = match_downloads([("Eminem", "Lose Yourself")], tmp_path)
    assert found == ["Eminem - Lose Yourself"]
    assert missing == []


def test_extra_finds_file_in_subfolder_not_in_playlist(tmp_path):
    """Same subfoldered-download scenario as
    test_matches_downloaded_file_in_subfolder, but for the reverse direction:
    a file sitting in a subfolder that matches no track in the loaded
    playlist must still surface in the "IN NEWMUSIC, NOT IN THIS PLAYLIST"
    section, not silently disappear."""
    album_dir = tmp_path / "Some Album"
    album_dir.mkdir()
    (album_dir / "Drake - Hotline Bling.mp3").write_bytes(b"")
    extra = find_extra_newmusic_files([("Eminem", "Lose Yourself")], tmp_path)
    assert extra == [("Drake", "Hotline Bling", album_dir / "Drake - Hotline Bling.mp3")]


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


# ------------------------------------------------ genuine boundary-crossing coverage
#
# The test above (test_spotify_client_constructs_real_client_when_config_present)
# substitutes an inline FakeRealSpotifyClient for the real class, so the actual
# spotify_tools.spotify_client.RealSpotifyClient is never constructed - the
# sibling-repo import is exercised, but nothing on the far side of it runs for
# real. These two tests close that gap: neither mocks anything inside
# spotify_tools. The first constructs the real production class; the second
# runs AudioManager's real orchestration function against a real (unmocked)
# SpotifyTools SpotifyInterface implementation end-to-end.


def test_spotify_client_constructs_a_real_unmocked_real_spotify_client(monkeypatch):
    """RealSpotifyClient itself, not a fake standing in for it, is constructed.

    RealSpotifyClient.__init__ builds a spotipy.oauth2.SpotifyOAuth and a
    spotipy.Spotify - both are network-lazy (no HTTP call happens until a
    method like .current_user() is invoked), so this is real, unmocked
    cross-repo construction with a throwaway config and zero network access.
    """
    fake_cfg = {
        "spotify_client_id": "test-id",
        "spotify_client_secret": "test-secret",
        "spotify_redirect_uri": "http://localhost:8888/callback",
    }
    monkeypatch.setattr(spotify_config, "load_config", lambda path: fake_cfg)

    client = _spotify_client()

    assert isinstance(client, spotify_client_module.RealSpotifyClient)
    import spotipy
    assert isinstance(client._sp, spotipy.Spotify)


def test_do_sync_liked_moves_liked_tracks_via_a_real_simulated_client(monkeypatch):
    """_do_sync_liked() -> spotify_tools.acquire.move_liked_to_playlist() ->
    a real (unmocked) SimulatedSpotifyClient - the same SpotifyInterface
    implementation SpotifyTools's own golden-path tests exercise. Only
    _spotify_client() is monkeypatched (it always builds a RealSpotifyClient,
    which needs real credentials); everything past that point - the shared
    acquire.py orchestration function, the SpotifyInterface ABC, the in-memory
    playlist/liked-songs state - is real SpotifyTools code, genuinely crossing
    the sibling-repo boundary rather than being mocked away on either side.
    """
    sim = SimulatedSpotifyClient(fixture_data={
        "search_responses": {},
        "initial_playlist": [],
        "initial_liked": ["spotify:track:aaa", "spotify:track:bbb"],
    })
    monkeypatch.setattr(acquire_module, "_spotify_client", lambda: sim)

    def _fake_save(*args, **kwargs):
        pass
    monkeypatch.setattr(acquire_module, "_save_last_playlist", _fake_save)

    msg = _do_sync_liked()

    assert sim.get_liked_track_uris() == set()  # both tracks removed from Liked
    assert set(sim.playlist_contents) == {"spotify:track:aaa", "spotify:track:bbb"}
    assert "Moved 2 track(s)" in msg
    assert "AudioManager Inbox" in msg


# ---------------------------------------------------- Load Liked Songs (_do_fetch_liked_tracks)


def test_do_fetch_liked_tracks_returns_same_row_shape_as_playlist_fetch(tmp_path, monkeypatch):
    """get_liked_tracks_detailed() returns the identical {artist, title, album,
    year, duration_ms} row shape as get_playlist_tracks_detailed() (shared
    _detail_row helper in SpotifyTools) - confirms _do_fetch_liked_tracks()
    turns that into the same 6-tuple _do_fetch_tracks() does, so every
    downstream consumer (track_table, match_downloads) needs no branching."""
    _isolate_state_file(tmp_path, monkeypatch)

    class FakeClient:
        def get_liked_tracks_detailed(self):
            return [
                {"artist": "Eminem", "title": "Lose Yourself", "album": "8 Mile",
                 "year": "2002", "duration_ms": 320000},
                {"artist": "Dua Lipa", "title": "Levitating", "album": "Future Nostalgia",
                 "year": "2020", "duration_ms": 203000},
            ]

    monkeypatch.setattr(acquire_module, "_spotify_client", lambda: FakeClient())

    from spotify_tools.open_playlist import _build_deemix_url

    rows = _do_fetch_liked_tracks()

    assert rows == [
        ("Eminem", "Lose Yourself", "8 Mile", "2002", "5:20", _build_deemix_url("Eminem", "Lose Yourself")),
        ("Dua Lipa", "Levitating", "Future Nostalgia", "2020", "3:23", _build_deemix_url("Dua Lipa", "Levitating")),
    ]


def test_do_fetch_liked_tracks_persists_under_the_sentinel_id(tmp_path, monkeypatch):
    """Persistence key must be _LIKED_SONGS_ID, not a real playlist id - so it
    round-trips through _load_last_playlist_id()/restore_cached_tracks() the
    same way a playlist fetch does, without colliding with one."""
    _isolate_state_file(tmp_path, monkeypatch)

    class FakeClient:
        def get_liked_tracks_detailed(self):
            return []

    monkeypatch.setattr(acquire_module, "_spotify_client", lambda: FakeClient())

    _do_fetch_liked_tracks()

    assert _load_last_playlist_id() == _LIKED_SONGS_ID
    assert _load_history() == [{"id": _LIKED_SONGS_ID, "name": _LIKED_SONGS_NAME}]


def test_liked_songs_sentinel_cannot_collide_with_a_real_playlist_id():
    """Real Spotify playlist/base62 ids are alphanumeric - the sentinel's
    underscores and double-underscore wrapper can never be produced by one."""
    assert not _LIKED_SONGS_ID.isalnum()


# ------------------------------------------------- duration + sort-key coverage


def test_format_duration_converts_ms_to_m_ss():
    assert _format_duration(225000) == "3:45"


def test_format_duration_zero_pads_seconds():
    """Regression guard on the display format the Length column and
    _length_to_seconds both assume: seconds are always two digits."""
    assert _format_duration(125000) == "2:05"


def test_format_duration_of_zero_is_zero_zero_zero():
    assert _format_duration(0) == "0:00"


def test_format_duration_round_trips_through_length_to_seconds():
    for ms in (0, 59000, 125000, 225000, 3599000):
        assert _length_to_seconds(_format_duration(ms)) == ms // 1000


def test_sorted_tracks_by_length_sorts_numerically_not_lexically():
    """"10:00" sorts BEFORE "2:00" as plain text - the Length column must use
    the seconds key, or a long track lands above a short one."""
    _state["tracks"] = [("A", "T1", "Alb", "2020", "2:00", ""), ("B", "T2", "Alb", "2020", "10:00", "")]
    _state["sort_col"] = "Length"
    _state["sort_reverse"] = False
    assert [row[1][4] for row in _sorted_tracks()] == ["2:00", "10:00"]


def test_sorted_tracks_by_length_reversed():
    _state["tracks"] = [("A", "T1", "Alb", "2020", "2:00", ""), ("B", "T2", "Alb", "2020", "10:00", "")]
    _state["sort_col"] = "Length"
    _state["sort_reverse"] = True
    assert [row[1][4] for row in _sorted_tracks()] == ["10:00", "2:00"]


def test_sorted_tracks_year_blanks_stay_last_when_reversed():
    """Blanks-last is a deliberate asymmetry: reversing the sort must not
    float empty Year cells to the top of the table."""
    _state["tracks"] = [
        ("A", "T1", "Alb", "", "1:00", ""),
        ("B", "T2", "Alb", "2020", "1:00", ""),
        ("C", "T3", "Alb", "1999", "1:00", ""),
    ]
    _state["sort_col"] = "Year"
    _state["sort_reverse"] = True
    assert [row[1][3] for row in _sorted_tracks()] == ["", "2020", "1999"]


def test_sorted_tracks_by_artist_is_case_insensitive():
    _state["tracks"] = [("beta", "T1", "Alb", "", "1:00", ""), ("Alpha", "T2", "Alb", "", "1:00", "")]
    _state["sort_col"] = "Artist"
    _state["sort_reverse"] = False
    assert [row[1][0] for row in _sorted_tracks()] == ["Alpha", "beta"]


# ------------------------------------------------- progress bar segment maths


def test_progress_metrics_widths_are_shares_of_the_three_counts():
    metrics = progress_metrics(downloaded=2, missing=2, extra=1)
    assert metrics["total"] == 5
    assert metrics["widths"] == [40.0, 40.0, 20.0]


def test_progress_metrics_widths_sum_to_one_hundred():
    for counts in ((1, 0, 0), (3, 7, 5), (60, 1, 2), (0, 0, 9)):
        widths = progress_metrics(*counts)["widths"]
        assert round(sum(widths), 6) == 100.0


def test_progress_metrics_all_zero_gives_zero_total_and_zero_widths():
    metrics = progress_metrics(0, 0, 0)
    assert metrics["total"] == 0
    assert metrics["widths"] == [0.0, 0.0, 0.0]
    assert metrics["pct_complete"] == 0


def test_progress_metrics_pct_complete_ignores_extra_files():
    """Extra files sit in NewMusic but are not part of the loaded playlist -
    counting them would let unrelated downloads inflate playlist completion.
    3 of 4 playlist tracks downloaded is 75% regardless of how many extras."""
    assert progress_metrics(3, 1, 0)["pct_complete"] == 75
    assert progress_metrics(3, 1, 50)["pct_complete"] == 75


def test_progress_metrics_pct_complete_is_zero_with_no_playlist_tracks():
    """Browse mode: extras only, no playlist - no division by zero, 0%."""
    assert progress_metrics(0, 0, 12)["pct_complete"] == 0


def test_progress_metrics_pct_complete_rounds_to_whole_percent():
    assert progress_metrics(1, 2, 0)["pct_complete"] == 33
    assert progress_metrics(2, 1, 0)["pct_complete"] == 67


def test_progress_metrics_full_completion_is_one_hundred_percent():
    assert progress_metrics(5, 0, 0)["pct_complete"] == 100


def test_segment_label_shown_only_once_segment_is_wide_enough():
    """Below the threshold progress_bar() falls back to a title tooltip
    rather than squeezing text into a sliver."""
    assert _segment_shows_label(12.0) is True
    assert _segment_shows_label(50.0) is True
    assert _segment_shows_label(11.9) is False


def test_narrow_extra_segment_falls_back_to_tooltip():
    """Real shape from the UI: a couple of extras next to 60+ downloaded."""
    widths = progress_metrics(downloaded=60, missing=2, extra=2)["widths"]
    assert _segment_shows_label(widths[0]) is True
    assert _segment_shows_label(widths[2]) is False


# ------------------------------------------------- header/cell/link labels


def test_header_label_idle_shows_sortability_hint():
    assert _header_label("Artist", None, False) == "Artist ⇅"


def test_header_label_idle_hint_shown_for_any_other_active_column():
    assert _header_label("Title", "Artist", False) == "Title ⇅"


def test_header_label_active_ascending_shows_up_arrow():
    assert _header_label("Artist", "Artist", False) == "Artist ▲"


def test_header_label_active_descending_shows_down_arrow():
    assert _header_label("Artist", "Artist", True) == "Artist ▼"


def test_downloaded_cell_text_true_is_a_checkmark_badge():
    assert _downloaded_cell_text(True) == "✓ Downloaded"


def test_downloaded_cell_text_false_is_a_plain_dash():
    assert _downloaded_cell_text(False) == "-"


def test_deemix_link_label_names_the_destination():
    assert _DEEMIX_LINK_LABEL == "Open in Deemix"


# ------------------------------------------- track/downloaded disk persistence


def _isolate_state_file(tmp_path, monkeypatch):
    """Points config.CACHE_DIR/ACQUIRE_STATE_JSON and NEWMUSIC_DIR at tmp_path
    so persistence tests never touch the real cache or the real NewMusic
    inbox (the GUI's read-only contract, gui/config.py)."""
    monkeypatch.setattr(config, "CACHE_DIR", tmp_path)
    monkeypatch.setattr(config, "ACQUIRE_STATE_JSON", tmp_path / "acquire-state.json")
    monkeypatch.setattr(config, "NEWMUSIC_DIR", tmp_path / "newmusic")


TRACKS = [
    ("Eminem", "Lose Yourself", "8 Mile", "2002", "5:20", "http://x/1"),
    ("Dua Lipa", "Levitating", "Future Nostalgia", "2020", "3:23", "http://x/2"),
]
DOWNLOADED = {"eminem:lose yourself": True, "dua lipa:levitating": False}


def test_tracks_cache_round_trips_through_disk(tmp_path, monkeypatch):
    _isolate_state_file(tmp_path, monkeypatch)
    _save_tracks_cache("pl1", TRACKS, DOWNLOADED)
    tracks, downloaded = _load_tracks_cache("pl1")
    assert tracks == TRACKS  # JSON lists come back as tuples, the shape track_table() unpacks
    assert downloaded == DOWNLOADED


def test_tracks_cache_is_keyed_by_playlist_id(tmp_path, monkeypatch):
    _isolate_state_file(tmp_path, monkeypatch)
    _save_last_playlist("pl1", "First")
    _save_last_playlist("pl2", "Second")
    _save_tracks_cache("pl1", TRACKS, DOWNLOADED)
    _save_tracks_cache("pl2", TRACKS[:1], {"eminem:lose yourself": False})
    assert len(_load_tracks_cache("pl1")[0]) == 2
    assert len(_load_tracks_cache("pl2")[0]) == 1
    assert _load_tracks_cache("pl1")[1]["eminem:lose yourself"] is True


def test_tracks_cache_unknown_playlist_returns_empty(tmp_path, monkeypatch):
    _isolate_state_file(tmp_path, monkeypatch)
    _save_tracks_cache("pl1", TRACKS, DOWNLOADED)
    assert _load_tracks_cache("pl-never-fetched") == ([], {})


def test_tracks_cache_missing_state_file_returns_empty(tmp_path, monkeypatch):
    _isolate_state_file(tmp_path, monkeypatch)
    assert _load_tracks_cache("pl1") == ([], {})


def test_tracks_cache_corrupt_state_file_returns_empty(tmp_path, monkeypatch):
    """A bad cache must degrade to "nothing restored", never break tab build."""
    _isolate_state_file(tmp_path, monkeypatch)
    config.ACQUIRE_STATE_JSON.write_text("{not json at all", encoding="utf-8")
    assert _load_tracks_cache("pl1") == ([], {})


def test_tracks_cache_survives_a_later_history_write(tmp_path, monkeypatch):
    """_save_last_playlist rewrites the same JSON file - it must not drop the
    cache block written beside it."""
    _isolate_state_file(tmp_path, monkeypatch)
    _save_last_playlist("pl1", "First")
    _save_tracks_cache("pl1", TRACKS, DOWNLOADED)
    _save_last_playlist("pl1", "First Renamed")
    assert _load_tracks_cache("pl1")[0] == TRACKS


def test_saving_the_cache_preserves_playlist_id_and_history(tmp_path, monkeypatch):
    _isolate_state_file(tmp_path, monkeypatch)
    _save_last_playlist("pl1", "First")
    _save_tracks_cache("pl1", TRACKS, DOWNLOADED)
    assert _load_history() == [{"id": "pl1", "name": "First"}]


def test_tracks_cache_is_pruned_to_playlists_still_in_history(tmp_path, monkeypatch):
    """The file can never grow without bound: once a playlist ages out of the
    capped history, its cached tracks go with it."""
    _isolate_state_file(tmp_path, monkeypatch)
    for i in range(6):
        _save_last_playlist(f"pl{i}", f"Playlist {i}")
        _save_tracks_cache(f"pl{i}", TRACKS, DOWNLOADED)
    assert _load_tracks_cache("pl0") == ([], {})  # aged out of the last-5 history
    assert _load_tracks_cache("pl5")[0] == TRACKS


def test_empty_playlist_id_is_never_cached(tmp_path, monkeypatch):
    _isolate_state_file(tmp_path, monkeypatch)
    _save_tracks_cache("", TRACKS, DOWNLOADED)
    assert _load_tracks_cache("") == ([], {})


# --------------------------------------------------- restore on tab rebuild


def _blank_state():
    _state["tracks"] = []
    _state["downloaded"] = {}
    _state["extra"] = []
    _state["sort_col"] = None
    _state["sort_reverse"] = False
    _state["playlist_loaded"] = False
    _state["simulated"] = False
    _state["manual_override"] = {}
    _state["fetch_progress"] = None
    _state["sync_busy"] = False


def test_restore_reloads_tracks_and_ticks_after_a_tab_rebuild(tmp_path, monkeypatch):
    """The whole point of the persistence item: a browser reload used to come
    back to an empty table."""
    _isolate_state_file(tmp_path, monkeypatch)
    _save_last_playlist("pl1", "First")
    _save_tracks_cache("pl1", TRACKS, DOWNLOADED)
    _blank_state()
    assert restore_cached_tracks() is True
    assert _state["tracks"] == TRACKS
    assert _state["downloaded"] == DOWNLOADED
    assert _state["playlist_loaded"] is True


def test_restore_is_a_noop_with_nothing_cached(tmp_path, monkeypatch):
    _isolate_state_file(tmp_path, monkeypatch)
    _blank_state()
    assert restore_cached_tracks() is False
    assert _state["tracks"] == []
    assert _state["playlist_loaded"] is False


def test_restore_never_clobbers_live_tracks(tmp_path, monkeypatch):
    _isolate_state_file(tmp_path, monkeypatch)
    _save_last_playlist("pl1", "First")
    _save_tracks_cache("pl1", TRACKS, DOWNLOADED)
    _blank_state()
    live = [("Live", "Track", "Alb", "2024", "1:00", "")]
    _state["tracks"] = live
    assert restore_cached_tracks() is False
    assert _state["tracks"] == live


def test_restore_never_clobbers_simulate_mode(tmp_path, monkeypatch):
    """Simulate's synthetic data must not be replaced by a real cached
    playlist, mirroring the _poll_should_skip guard."""
    _isolate_state_file(tmp_path, monkeypatch)
    _save_last_playlist("pl1", "First")
    _save_tracks_cache("pl1", TRACKS, DOWNLOADED)
    _blank_state()
    _state["simulated"] = True
    assert restore_cached_tracks() is False
    assert _state["tracks"] == []


# ------------------------------------------------------------------ clear()


def test_clear_resets_sort_state_and_refreshes_history(tmp_path, monkeypatch):
    """Regression for the noted inconsistency: Clear left _state["sort_col"]/
    ["sort_reverse"] set and never refreshed the history menu, unlike fetch()."""
    _isolate_state_file(tmp_path, monkeypatch)
    refreshed = []
    monkeypatch.setitem(acquire_module._refresh_hooks, "track_table", lambda: refreshed.append("track_table"))
    monkeypatch.setitem(acquire_module._refresh_hooks, "progress_bar", lambda: refreshed.append("progress_bar"))
    monkeypatch.setitem(acquire_module._refresh_hooks, "history_items", lambda: refreshed.append("history_items"))
    monkeypatch.setitem(acquire_module._refresh_hooks, "simulate_banner", lambda: refreshed.append("simulate_banner"))

    _state["tracks"] = list(TRACKS)
    _state["downloaded"] = dict(DOWNLOADED)
    _state["sort_col"] = "Year"
    _state["sort_reverse"] = True
    _state["playlist_loaded"] = True
    _state["simulated"] = True

    clear_tab_state()

    assert _state["tracks"] == []
    assert _state["downloaded"] == {}
    assert _state["sort_col"] is None
    assert _state["sort_reverse"] is False
    assert _state["playlist_loaded"] is False
    assert _state["simulated"] is False
    assert sorted(refreshed) == ["history_items", "progress_bar", "simulate_banner", "track_table"]


def test_clear_forgets_the_cached_playlist_so_a_rebuild_stays_blank(tmp_path, monkeypatch):
    """Without this, restore_cached_tracks() would put back on the next tab
    build exactly what Clear just removed."""
    _isolate_state_file(tmp_path, monkeypatch)
    _save_last_playlist("pl1", "First")
    _save_tracks_cache("pl1", TRACKS, DOWNLOADED)
    _state["tracks"] = list(TRACKS)
    _state["playlist_loaded"] = True

    clear_tab_state()

    assert _load_tracks_cache("pl1") == ([], {})
    _blank_state()
    assert restore_cached_tracks() is False


def test_clear_keeps_the_playlist_history(tmp_path, monkeypatch):
    """Clear resets the view, it does not erase where you have been."""
    _isolate_state_file(tmp_path, monkeypatch)
    _save_last_playlist("pl1", "First")
    _save_last_playlist("pl2", "Second")
    _blank_state()

    clear_tab_state()

    assert [h["id"] for h in _load_history()] == ["pl2", "pl1"]


def test_clear_leaves_the_newmusic_extras_section_intact(tmp_path, monkeypatch):
    """Clear rescans NewMusic rather than blanking it - the extra-files view
    is not part of the fetched playlist and must survive."""
    _isolate_state_file(tmp_path, monkeypatch)
    newmusic = tmp_path / "newmusic"
    newmusic.mkdir()
    (newmusic / "Drake - Hotline Bling.mp3").write_bytes(b"")
    _state["tracks"] = list(TRACKS)
    _state["playlist_loaded"] = True

    clear_tab_state()

    assert [(a, t) for a, t, _url, _path in _state["extra"]] == [("Drake", "Hotline Bling")]


def test_fetch_state_is_persisted_by_the_downloads_check(tmp_path, monkeypatch):
    """End-to-end of the persistence path as fetch() drives it: tracks land in
    _state, the NewMusic check runs, and the result is on disk for the next
    tab build - no separate save call in fetch() to forget."""
    _isolate_state_file(tmp_path, monkeypatch)
    newmusic = tmp_path / "newmusic"
    newmusic.mkdir()
    (newmusic / "Eminem - Lose Yourself.mp3").write_bytes(b"")
    _save_last_playlist("pl1", "First")
    _blank_state()
    _state["tracks"] = list(TRACKS)
    _state["playlist_loaded"] = True

    acquire_module._run_check_against_downloads()

    cached_tracks, cached_downloaded = _load_tracks_cache("pl1")
    assert cached_tracks == TRACKS
    assert cached_downloaded["eminem:lose yourself"] is True
    assert cached_downloaded["dua lipa:levitating"] is False


def test_simulate_state_is_never_persisted(tmp_path, monkeypatch):
    """Synthetic sample data must never reach the on-disk cache, or a reload
    would restore fake tracks as if they were a real playlist."""
    _isolate_state_file(tmp_path, monkeypatch)
    _save_last_playlist("pl1", "First")
    simulate()

    acquire_module._run_check_against_downloads()

    assert _load_tracks_cache("pl1") == ([], {})


# ------------------------------------------------- Acquire tab polish (2026-09-05)
# manual override for verify-download false pos/neg (item 1)


def test_toggle_manual_override_flips_value_and_marks_the_row(tmp_path, monkeypatch):
    _isolate_state_file(tmp_path, monkeypatch)
    _blank_state()
    _state["tracks"] = list(TRACKS)
    _state["downloaded"] = dict(DOWNLOADED)
    _state["playlist_loaded"] = True

    row_key = "dua lipa:levitating"
    toggle_manual_override(row_key)

    assert _state["downloaded"][row_key] is True  # was False
    assert _state["manual_override"][row_key] is True

    toggle_manual_override(row_key)
    assert _state["downloaded"][row_key] is False  # flips back


def test_toggle_manual_override_persists_across_a_reload(tmp_path, monkeypatch):
    _isolate_state_file(tmp_path, monkeypatch)
    _blank_state()
    _save_last_playlist("pl1", "First")
    _state["tracks"] = list(TRACKS)
    _state["downloaded"] = dict(DOWNLOADED)
    _state["playlist_loaded"] = True

    toggle_manual_override("eminem:lose yourself")

    assert _load_manual_overrides("pl1") == {"eminem:lose yourself": True}


def test_check_against_downloads_skips_overridden_rows(tmp_path, monkeypatch):
    """The fuzzy match must never touch a row the user has manually set - even
    when the real NewMusic folder disagrees with the override."""
    _isolate_state_file(tmp_path, monkeypatch)
    newmusic = tmp_path / "newmusic"
    newmusic.mkdir()
    # Neither file exists on disk, so an un-overridden fuzzy match would mark both False.
    _blank_state()
    _state["tracks"] = list(TRACKS)
    _state["downloaded"] = {"eminem:lose yourself": True, "dua lipa:levitating": False}
    _state["manual_override"] = {"eminem:lose yourself": True}
    _state["playlist_loaded"] = True

    _run_check_against_downloads()

    assert _state["downloaded"]["eminem:lose yourself"] is True  # override kept, untouched by fuzzy match
    assert _state["downloaded"]["dua lipa:levitating"] is False  # freshly matched (no file -> False)


def test_check_against_downloads_still_matches_non_overridden_rows(tmp_path, monkeypatch):
    _isolate_state_file(tmp_path, monkeypatch)
    newmusic = tmp_path / "newmusic"
    newmusic.mkdir()
    (newmusic / "Dua Lipa - Levitating.mp3").write_bytes(b"")
    _blank_state()
    _state["tracks"] = list(TRACKS)
    _state["downloaded"] = {"eminem:lose yourself": False, "dua lipa:levitating": False}
    _state["manual_override"] = {"eminem:lose yourself": True}  # user says "not downloaded", stays False
    _state["playlist_loaded"] = True

    _run_check_against_downloads()

    assert _state["downloaded"]["eminem:lose yourself"] is False  # override honored despite no file either way
    assert _state["downloaded"]["dua lipa:levitating"] is True  # freshly matched, file exists


def test_clear_tab_state_forgets_manual_overrides(tmp_path, monkeypatch):
    _isolate_state_file(tmp_path, monkeypatch)
    _blank_state()
    _state["tracks"] = list(TRACKS)
    _state["manual_override"] = {"eminem:lose yourself": True}
    _state["playlist_loaded"] = True

    clear_tab_state()

    assert _state["manual_override"] == {}


def test_restore_cached_tracks_restores_manual_overrides(tmp_path, monkeypatch):
    _isolate_state_file(tmp_path, monkeypatch)
    _blank_state()
    _save_last_playlist("pl1", "First")
    _save_tracks_cache("pl1", TRACKS, DOWNLOADED, {"eminem:lose yourself": True})

    assert restore_cached_tracks() is True
    assert _state["manual_override"] == {"eminem:lose yourself": True}


# ------------------------------------------- row keys are content-based, not index-based (IDEAS.md, 2026-09-05)


def test_row_keys_are_independent_of_fetch_order():
    """Regression for IDEAS.md "Acquire's persisted row keys are index-based":
    the same set of tracks must produce the same set of row_keys whether or
    not the fetch reordered them - each key must depend only on that track's
    own artist/title content."""
    forward = acquire_module._row_keys_for_tracks(TRACKS)
    reversed_keys = acquire_module._row_keys_for_tracks(list(reversed(TRACKS)))
    assert set(forward) == set(reversed_keys)
    assert forward == list(reversed(reversed_keys))


def test_row_keys_disambiguate_genuine_duplicates():
    """Two tracks that normalise to the same content key (a real duplicate in
    the fetched list) must still get distinct, non-colliding keys."""
    dup_tracks = [
        ("Eminem", "Lose Yourself", "8 Mile", "2002", "5:20", "http://x/1"),
        ("Eminem", "Lose Yourself", "8 Mile", "2002", "5:20", "http://x/1"),
    ]
    keys = acquire_module._row_keys_for_tracks(dup_tracks)
    assert len(set(keys)) == 2


def test_manual_override_follows_the_track_after_a_reordered_refetch(tmp_path, monkeypatch):
    """The core regression: David marks a track's Downloaded cell as a
    manual override, then re-fetches a reordered playlist (a new track lands
    at the top upstream, shifting every later track's index). The override
    must keep applying to the SAME track by artist/title content, not to
    whatever track now sits at the old numeric index."""
    _isolate_state_file(tmp_path, monkeypatch)
    (tmp_path / "newmusic").mkdir()
    _blank_state()
    _state["tracks"] = list(TRACKS)  # [Eminem - Lose Yourself, Dua Lipa - Levitating]
    _state["playlist_loaded"] = True

    dua_lipa_key = build_track_rows()[1]["row_key"]
    toggle_manual_override(dua_lipa_key)
    assert _state["manual_override"] == {dua_lipa_key: True}
    assert _state["downloaded"][dua_lipa_key] is True

    # Simulate a re-fetch of a reordered playlist: a new track is now first,
    # pushing Dua Lipa from index 1 to index 2 and Eminem from 0 to 1.
    new_track = ("New Artist", "New Song", "New Album", "2024", "1:00", "http://x/3")
    _state["tracks"] = [new_track, TRACKS[0], TRACKS[1]]

    _run_check_against_downloads()  # no files on disk - a fresh match marks everything else False

    rows = {row["artist"]: row for row in build_track_rows()}
    assert rows["Dua Lipa"]["row_key"] == dua_lipa_key
    assert rows["Dua Lipa"]["is_downloaded"] is True  # override survived the reorder
    assert rows["Dua Lipa"]["is_overridden"] is True
    assert rows["Eminem"]["is_downloaded"] is False  # untouched by the override
    assert rows["Eminem"]["is_overridden"] is False


# ---------------------------------------------------------- fetch progress (item 2)


def test_do_fetch_tracks_reports_progress_per_track(tmp_path, monkeypatch):
    _isolate_state_file(tmp_path, monkeypatch)

    class FakeClient:
        def get_playlist_tracks_detailed(self, playlist_id):
            return [
                {"artist": "Eminem", "title": "Lose Yourself", "album": "8 Mile", "year": "2002", "duration_ms": 320000},
                {"artist": "Dua Lipa", "title": "Levitating", "album": "Future Nostalgia", "year": "2020", "duration_ms": 203000},
            ]

        def get_playlist_name(self, playlist_id):
            return "My Playlist"

    monkeypatch.setattr(acquire_module, "_spotify_client", lambda: FakeClient())

    seen = []
    _do_fetch_tracks("pl1", on_progress=lambda current, total: seen.append((current, total)))

    assert seen == [(1, 2), (2, 2)]


def test_do_fetch_tracks_progress_is_optional(tmp_path, monkeypatch):
    """Every pre-existing caller passes no on_progress at all - must not break."""
    _isolate_state_file(tmp_path, monkeypatch)

    class FakeClient:
        def get_playlist_tracks_detailed(self, playlist_id):
            return [{"artist": "A", "title": "B", "album": "C", "year": "2020", "duration_ms": 1000}]

        def get_playlist_name(self, playlist_id):
            return ""

    monkeypatch.setattr(acquire_module, "_spotify_client", lambda: FakeClient())

    rows = _do_fetch_tracks("pl1")
    assert len(rows) == 1


def test_do_fetch_liked_tracks_reports_progress_per_track(tmp_path, monkeypatch):
    _isolate_state_file(tmp_path, monkeypatch)

    class FakeClient:
        def get_liked_tracks_detailed(self):
            return [
                {"artist": "Eminem", "title": "Lose Yourself", "album": "8 Mile", "year": "2002", "duration_ms": 320000},
                {"artist": "Dua Lipa", "title": "Levitating", "album": "Future Nostalgia", "year": "2020", "duration_ms": 203000},
                {"artist": "Drake", "title": "Hotline Bling", "album": "Views", "year": "2016", "duration_ms": 267000},
            ]

    monkeypatch.setattr(acquire_module, "_spotify_client", lambda: FakeClient())

    seen = []
    _do_fetch_liked_tracks(on_progress=lambda current, total: seen.append((current, total)))

    assert seen == [(1, 3), (2, 3), (3, 3)]


# ------------------------------------------------------------- pagination (item 3)


def test_build_track_rows_shapes_every_fetched_track(tmp_path, monkeypatch):
    """ui.table's own pagination prop decides what renders - build_track_rows()
    itself must never slice or drop tracks from the full fetched list."""
    _isolate_state_file(tmp_path, monkeypatch)
    _blank_state()
    _state["tracks"] = list(TRACKS)
    _state["downloaded"] = dict(DOWNLOADED)

    rows = build_track_rows()

    assert len(rows) == 2
    assert rows[0]["row_key"] == "eminem:lose yourself"
    assert rows[0]["artist"] == "Eminem"
    assert rows[0]["is_downloaded"] is True
    assert rows[1]["is_downloaded"] is False


def test_build_track_rows_flags_overridden_rows(tmp_path, monkeypatch):
    _isolate_state_file(tmp_path, monkeypatch)
    _blank_state()
    _state["tracks"] = list(TRACKS)
    _state["downloaded"] = dict(DOWNLOADED)
    _state["manual_override"] = {"eminem:lose yourself": True}

    rows = build_track_rows()

    assert rows[0]["is_overridden"] is True
    assert rows[1]["is_overridden"] is False


def test_build_track_rows_respects_hide_downloaded(tmp_path, monkeypatch):
    _isolate_state_file(tmp_path, monkeypatch)
    _blank_state()
    _state["tracks"] = list(TRACKS)
    _state["downloaded"] = dict(DOWNLOADED)
    _state["hide_downloaded"] = True

    rows = build_track_rows()

    assert len(rows) == 1
    assert rows[0]["row_key"] == "dua lipa:levitating"
    _state["hide_downloaded"] = False  # don't leak into other tests sharing module state


# ------------------------------------------------------- dedupe guard for Sync Liked (item 4)


def test_run_sync_liked_second_call_while_busy_is_a_no_op(tmp_path, monkeypatch):
    """A second Sync Liked Songs click that lands while the first sync is still
    in flight must not fire a second overlapping sync - the model is
    gui/runner.py's `runner.busy` guard. Uses real threading.Events (not
    asyncio.gather scheduling order, which gives no ordering guarantee once
    the first call has already reached asyncio.to_thread) to force the second
    call to land deterministically while the first is genuinely mid-flight."""
    import threading

    _isolate_state_file(tmp_path, monkeypatch)
    _blank_state()
    calls = []
    started = threading.Event()
    release = threading.Event()

    def fake_sync():
        calls.append(1)
        started.set()
        release.wait(timeout=2)
        return "Moved 1 track(s)"

    monkeypatch.setattr(acquire_module, "_do_sync_liked", fake_sync)

    async def run_overlapping_clicks():
        task1 = asyncio.create_task(_run_sync_liked())
        await asyncio.to_thread(started.wait, 2)  # wait until the first sync is truly in flight
        await _run_sync_liked()  # the second click, landing mid-flight
        release.set()
        await task1

    asyncio.run(run_overlapping_clicks())

    assert calls == [1]  # only the first call's body ever ran
    assert _state["sync_busy"] is False  # guard cleared afterwards


def test_run_sync_liked_clears_busy_flag_on_failure(tmp_path, monkeypatch):
    _isolate_state_file(tmp_path, monkeypatch)
    _blank_state()

    def fake_sync():
        raise RuntimeError("boom")

    monkeypatch.setattr(acquire_module, "_do_sync_liked", fake_sync)

    messages = []
    asyncio.run(_run_sync_liked(messages.append))

    assert _state["sync_busy"] is False
    assert "Failed: boom" in messages[-1]


def test_run_sync_liked_reports_success_message(tmp_path, monkeypatch):
    _isolate_state_file(tmp_path, monkeypatch)
    _blank_state()
    monkeypatch.setattr(acquire_module, "_do_sync_liked", lambda: "Moved 2 track(s)")

    messages = []
    asyncio.run(_run_sync_liked(messages.append))

    assert messages[-1] == "Moved 2 track(s)"
    assert _state["sync_busy"] is False


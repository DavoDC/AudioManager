"""Unit tests for gui.tabs.acquire.match_downloads (pure, no filesystem writes)."""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
sys.path.insert(0, str(Path(__file__).resolve().parents[2].parent / "SpotifyPlaylistGen"))

from gui.tabs.acquire import match_downloads


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


def test_matches_when_fetched_track_has_extra_collab_artists(tmp_path):
    """Regression: a Spotify track's artist field can carry featured/collab
    artists ("DC The Don & Someone") that the downloaded filename never
    includes, so exact-artist equality missed a real match. Primary artist
    (text before " & ") plus a case-insensitive substring check now covers it."""
    (tmp_path / "DC THE DON - Yellow.mp3").write_bytes(b"")
    found, missing = match_downloads([("DC The Don & Someone", "Yellow")], tmp_path)
    assert found == ["DC The Don & Someone - Yellow"]
    assert missing == []

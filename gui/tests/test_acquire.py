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

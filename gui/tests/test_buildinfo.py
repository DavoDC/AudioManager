"""Build provenance: one line shared by footer, /api/build and CLI start-up."""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from gui import buildinfo


def test_format_line_has_repo_sha_date():
    line = buildinfo.format_line({"repo": "AudioManager", "sha": "abc1234", "date": "2026-10-12", "dirty": False})
    assert line == "AudioManager abc1234 2026-10-12"


def test_format_line_marks_dirty_and_fixture():
    info = {"repo": "AudioManager", "sha": "abc1234", "date": "2026-10-12", "dirty": True}
    assert buildinfo.format_line(info) == "AudioManager abc1234* 2026-10-12"
    assert buildinfo.format_line(info, fixture="empty").endswith("FIXTURE empty")


def test_format_line_unknown_outside_git():
    info = {"repo": "AudioManager", "sha": "unknown", "date": "unknown", "dirty": False}
    assert buildinfo.format_line(info) == "AudioManager unknown"


def test_api_payload_keys_and_stable_boot_id():
    a = buildinfo.api_payload()
    b = buildinfo.api_payload()
    assert set(a) >= {"line", "sha", "dirty", "date", "boot_id", "started_at"}
    assert a["boot_id"] == b["boot_id"]
    assert re.match(r"^AudioManager ", a["line"])


def test_describe_never_raises_for_missing_repo(tmp_path):
    info = buildinfo.describe(repo_root=tmp_path / "nope", refresh=True)
    assert info["sha"] == "unknown"

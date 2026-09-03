"""Unit tests for gui.routing - the exe's dry-run routing JSON contract
(parse_routing_file) and the JSON-path extraction regex that reads the exe's
stdout (routing_path_from_output). Both are pure and exe-output-format
sensitive, so a silent drift in either contract should fail here first."""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from gui import config, routing


# ------------------------------------------------------- parse_routing_file


def _write_json(path, data):
    path.write_text(json.dumps(data), encoding="utf-8")


def test_parse_routing_file_full_entry_round_trips_all_fields(tmp_path):
    path = tmp_path / "routing.json"
    _write_json(path, [{
        "filename": "Song.mp3", "artist": "Artist", "title": "Title", "album": "Album",
        "destination": "Artists/Artist", "reason": "clean", "isNewFolder": True,
        "status": "ok", "inBatchDuplicate": True, "tagChanges": ["title", "artist"],
    }])
    entries = routing.parse_routing_file(path)
    assert entries == [{
        "filename": "Song.mp3", "artist": "Artist", "title": "Title", "album": "Album",
        "destination": "Artists/Artist", "reason": "clean", "isNewFolder": True,
        "status": "ok", "inBatchDuplicate": True, "tagChanges": ["title", "artist"],
    }]


def test_parse_routing_file_missing_fields_default_to_safe_values(tmp_path):
    path = tmp_path / "routing.json"
    _write_json(path, [{"filename": "Song.mp3"}])
    entries = routing.parse_routing_file(path)
    assert entries == [{
        "filename": "Song.mp3", "artist": "", "title": "", "album": "",
        "destination": "", "reason": "", "isNewFolder": False,
        "status": "", "inBatchDuplicate": False, "tagChanges": [],
    }]


def test_parse_routing_file_drops_entries_missing_filename(tmp_path):
    path = tmp_path / "routing.json"
    _write_json(path, [{"filename": "keep.mp3"}, {"artist": "no filename"}])
    entries = routing.parse_routing_file(path)
    assert [e["filename"] for e in entries] == ["keep.mp3"]


def test_parse_routing_file_drops_non_dict_entries(tmp_path):
    path = tmp_path / "routing.json"
    _write_json(path, [{"filename": "keep.mp3"}, "a string", 123, None])
    entries = routing.parse_routing_file(path)
    assert [e["filename"] for e in entries] == ["keep.mp3"]


def test_parse_routing_file_non_list_root_returns_empty(tmp_path):
    path = tmp_path / "routing.json"
    _write_json(path, {"filename": "not-a-list"})
    assert routing.parse_routing_file(path) == []


def test_parse_routing_file_non_string_optional_fields_default_safely(tmp_path):
    """A malformed exe output (e.g. a JSON null instead of a string) must not
    crash the parser - it should fall back to the same default as a missing
    field, not propagate the wrong type into the GUI."""
    path = tmp_path / "routing.json"
    _write_json(path, [{"filename": "Song.mp3", "artist": None, "tagChanges": [1, "title", None]}])
    entries = routing.parse_routing_file(path)
    assert entries[0]["artist"] == ""
    assert entries[0]["tagChanges"] == ["title"]


def test_parse_routing_file_handles_utf8_bom(tmp_path):
    path = tmp_path / "routing.json"
    path.write_bytes(b"\xef\xbb\xbf" + json.dumps([{"filename": "Song.mp3"}]).encode("utf-8"))
    entries = routing.parse_routing_file(path)
    assert [e["filename"] for e in entries] == ["Song.mp3"]


# --------------------------------------------------- routing_path_from_output


def test_routing_path_from_output_extracts_path_when_file_exists(tmp_path, monkeypatch):
    json_path = tmp_path / "routing-20260903-120000.json"
    json_path.write_text("[]", encoding="utf-8")
    lines = ["Some other output", f"  JSON: {json_path}", "Done"]
    assert routing.routing_path_from_output(lines) == json_path


def test_routing_path_from_output_ignores_matched_path_that_does_not_exist(tmp_path, monkeypatch):
    """A matched 'JSON: <path>' line pointing at a file that isn't actually
    there must not be trusted - fall back to scanning LOGS_DIR instead of
    handing back a dead path."""
    monkeypatch.setattr(config, "LOGS_DIR", tmp_path)
    real = tmp_path / "routing-20260903-090000.json"
    real.write_text("[]", encoding="utf-8")
    missing = tmp_path / "routing-20260903-120000.json"
    lines = [f"  JSON: {missing}"]
    assert routing.routing_path_from_output(lines) == real


def test_routing_path_from_output_falls_back_to_newest_in_logs_dir(tmp_path, monkeypatch):
    monkeypatch.setattr(config, "LOGS_DIR", tmp_path)
    older = tmp_path / "routing-20260901-000000.json"
    newer = tmp_path / "routing-20260902-000000.json"
    older.write_text("[]", encoding="utf-8")
    newer.write_text("[]", encoding="utf-8")
    import os
    import time
    os.utime(older, (time.time() - 100, time.time() - 100))
    assert routing.routing_path_from_output([]) == newer


def test_routing_path_from_output_returns_none_when_nothing_found(tmp_path, monkeypatch):
    monkeypatch.setattr(config, "LOGS_DIR", tmp_path)
    assert routing.routing_path_from_output(["no json line here"]) is None


# ------------------------------------------------- parse_projected_libchecker


def test_parse_projected_libchecker_returns_none_when_section_absent():
    assert routing.parse_projected_libchecker(["some", "other", "output"]) is None


def test_parse_projected_libchecker_clean_run():
    lines = [
        "===========================================================================",
        "Projected LibChecker (Dry Run)",
        "===========================================================================",
        " - Projected library: 412 current, -0 removals, +6 additions = 418 projected",
        " - Checking all tags against filenames..",
        " - LibChecker: Clean",
        "",
        " - Time taken: 00:00:01.2340000",
    ]
    v = routing.parse_projected_libchecker(lines)
    assert v == {
        "summary": "Projected library: 412 current, -0 removals, +6 additions = 418 projected",
        "clean": True, "skipped": False, "total_hits": 0,
    }


def test_parse_projected_libchecker_dirty_run_sums_total_hits():
    lines = [
        "Projected LibChecker (Dry Run)",
        " - Projected library: 412 current, -0 removals, +6 additions = 418 projected",
        " - Checking all tags against filenames..",
        "  - 'Song.mp3' has no title set!",
        "  - Total hits: 1",
        " - Checking for duplicates...",
        "  - 'Other.mp3' duplicates another track!",
        "  - Total hits: 2",
        " - Time taken: 00:00:01.2340000",
    ]
    v = routing.parse_projected_libchecker(lines)
    assert v["clean"] is False
    assert v["skipped"] is False
    assert v["total_hits"] == 3


def test_parse_projected_libchecker_skip_when_library_tags_unloadable():
    lines = [
        "Projected LibChecker (Dry Run)",
        " - SKIP: could not load current library tags: file not found",
    ]
    v = routing.parse_projected_libchecker(lines)
    assert v["skipped"] is True
    assert v["clean"] is False


# --------------------------------------------------- parse_confidence_report


def test_parse_confidence_report_returns_none_when_section_absent():
    assert routing.parse_confidence_report(["some", "other", "output"]) is None


def test_parse_confidence_report_clean_run():
    lines = [
        "===========================================================================",
        "  CONFIDENCE REPORT",
        "===========================================================================",
        "",
        "  Files in NewMusic: 12  |  Moved: 12  |  Skipped: 0",
        "  [MOVED] Song.mp3",
        "    -> Artists/Artist/Song.mp3",
        "",
        "  Sanity check: all 12 moved file(s) exist and are readable.",
    ]
    v = routing.parse_confidence_report(lines)
    assert v == {
        "count_line": "Files in NewMusic: 12  |  Moved: 12  |  Skipped: 0",
        "count_ok": True, "sanity_ok": True,
        "sanity_summary": "Sanity check: all 12 moved file(s) exist and are readable.",
        "error_count": 0,
    }


def test_parse_confidence_report_count_mismatch():
    lines = [
        "CONFIDENCE REPORT",
        "  Files in NewMusic: 12  |  Moved: 10  |  Skipped: 0",
        "  [ERROR] Count mismatch! Expected 12 moved, got 10.",
    ]
    v = routing.parse_confidence_report(lines)
    assert v["count_ok"] is False


def test_parse_confidence_report_sanity_check_failed():
    lines = [
        "CONFIDENCE REPORT",
        "  Files in NewMusic: 2  |  Moved: 2  |  Skipped: 0",
        "",
        "  [ERROR] Destination sanity check FAILED:",
        "  [MISSING] Artists/Artist/Song.mp3",
    ]
    v = routing.parse_confidence_report(lines)
    assert v["count_ok"] is True
    assert v["sanity_ok"] is False


def test_parse_confidence_report_error_summary_count():
    lines = [
        "CONFIDENCE REPORT",
        "  Files in NewMusic: 1  |  Moved: 0  |  Skipped: 0",
        "[ERRORS: 1]",
        "- Song.mp3: could not read tags",
    ]
    v = routing.parse_confidence_report(lines)
    assert v["error_count"] == 1


# ------------------------------------------------------------- newmusic_path


def test_newmusic_path_joins_newmusic_dir(monkeypatch, tmp_path):
    monkeypatch.setattr(config, "NEWMUSIC_DIR", tmp_path)
    assert routing.newmusic_path("Artist - Song.mp3") == tmp_path / "Artist - Song.mp3"


def test_newmusic_path_preserves_relative_subfolders(monkeypatch, tmp_path):
    monkeypatch.setattr(config, "NEWMUSIC_DIR", tmp_path)
    assert routing.newmusic_path("Sub/Song.mp3") == tmp_path / "Sub" / "Song.mp3"

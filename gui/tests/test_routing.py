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
        "status": "ok", "inBatchDuplicate": True, "compilationAlbum": True,
        "tagChanges": ["title", "artist"],
        "libraryDuplicate": True, "dupLibraryPath": "Artists/Artist/Album/Song.mp3",
        "dupLibraryTrack": "Title", "dupLibraryAlbum": "Album", "dupNewAlbum": "Album (Deluxe)",
        "dupRecommendationKey": "L", "dupRecommendation": "Delete library copy",
        "dupReason": "deluxe preferred",
    }])
    entries = routing.parse_routing_file(path)
    assert entries == [{
        "filename": "Song.mp3", "artist": "Artist", "title": "Title", "album": "Album",
        "destination": "Artists/Artist", "reason": "clean", "isNewFolder": True,
        "status": "ok", "inBatchDuplicate": True, "compilationAlbum": True,
        "tagChanges": ["title", "artist"],
        "libraryDuplicate": True, "dupLibraryPath": "Artists/Artist/Album/Song.mp3",
        "dupLibraryTrack": "Title", "dupLibraryAlbum": "Album", "dupNewAlbum": "Album (Deluxe)",
        "dupRecommendationKey": "L", "dupRecommendation": "Delete library copy",
        "dupReason": "deluxe preferred",
    }]


def test_parse_routing_file_missing_fields_default_to_safe_values(tmp_path):
    path = tmp_path / "routing.json"
    _write_json(path, [{"filename": "Song.mp3"}])
    entries = routing.parse_routing_file(path)
    assert entries == [{
        "filename": "Song.mp3", "artist": "", "title": "", "album": "",
        "destination": "", "reason": "", "isNewFolder": False,
        "status": "", "inBatchDuplicate": False, "compilationAlbum": False,
        "tagChanges": [],
        "libraryDuplicate": False, "dupLibraryPath": "", "dupLibraryTrack": "",
        "dupLibraryAlbum": "", "dupNewAlbum": "", "dupRecommendationKey": "",
        "dupRecommendation": "", "dupReason": "",
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


# ------------------------------------------------ batch summary (both shapes)


def _doc(files=None, summary=None):
    d = {"files": files if files is not None else []}
    if summary is not None:
        d["summary"] = summary
    return d


def test_parse_routing_file_reads_files_from_the_summary_shape(tmp_path):
    path = tmp_path / "routing.json"
    _write_json(path, _doc([{"filename": "Song.mp3"}], {"routes": {"Artists": 1}}))
    assert [e["filename"] for e in routing.parse_routing_file(path)] == ["Song.mp3"]


def test_parse_routing_file_still_reads_a_bare_array_from_an_older_exe(tmp_path):
    path = tmp_path / "routing.json"
    _write_json(path, [{"filename": "Old.mp3"}])
    assert [e["filename"] for e in routing.parse_routing_file(path)] == ["Old.mp3"]


def test_parse_batch_summary_reads_all_four_fields(tmp_path):
    path = tmp_path / "routing.json"
    _write_json(path, _doc([], {
        "routes": {"Artists": 12, "Compilations": 2},
        "miscAutoMigrations": [{"artist": "Hopsin", "count": 3}],
        "miscAutoMigrationTotal": 3,
        "compilationAlbums": ["Now 42"],
    }))
    s = routing.parse_batch_summary(path)
    assert s["routes"] == {"Artists": 12, "Compilations": 2}
    assert s["miscAutoMigrations"] == [{"artist": "Hopsin", "count": 3}]
    assert s["miscAutoMigrationTotal"] == 3
    assert s["compilationAlbums"] == ["Now 42"]


def test_parse_batch_summary_bare_array_yields_the_empty_summary(tmp_path):
    """An older exe's routing JSON has no batch context - the GUI must get the
    full key set with everything empty, never a KeyError."""
    path = tmp_path / "routing.json"
    _write_json(path, [{"filename": "Old.mp3"}])
    assert routing.parse_batch_summary(path) == routing.EMPTY_SUMMARY


def test_parse_batch_summary_missing_or_malformed_summary_is_empty(tmp_path):
    path = tmp_path / "routing.json"
    _write_json(path, _doc([{"filename": "A.mp3"}], "not a dict"))
    assert routing.parse_batch_summary(path) == routing.EMPTY_SUMMARY


def test_parse_batch_summary_drops_malformed_rows_and_nonpositive_counts(tmp_path):
    path = tmp_path / "routing.json"
    _write_json(path, _doc([], {
        "routes": {"Artists": 3, "Ghost": 0, "Bad": "many", "": 1},
        "miscAutoMigrations": [
            {"artist": "Good", "count": 2},
            {"artist": "Zero", "count": 0},
            {"artist": "", "count": 4},
            {"count": 9},
            "junk",
        ],
        "compilationAlbums": ["Real", "", None, 7],
    }))
    s = routing.parse_batch_summary(path)
    assert s["routes"] == {"Artists": 3}
    assert s["miscAutoMigrations"] == [{"artist": "Good", "count": 2}]
    assert s["compilationAlbums"] == ["Real"]


def test_parse_batch_summary_recomputes_a_malformed_total_from_the_rows(tmp_path):
    """The total is a convenience, the rows are the truth - a total that
    disagrees with the rows must never be the number the GUI shows."""
    path = tmp_path / "routing.json"
    _write_json(path, _doc([], {
        "miscAutoMigrations": [{"artist": "A", "count": 2}, {"artist": "B", "count": 3}],
        "miscAutoMigrationTotal": "lots",
    }))
    assert routing.parse_batch_summary(path)["miscAutoMigrationTotal"] == 5


def test_parse_batch_summary_booleans_are_not_accepted_as_counts(tmp_path):
    path = tmp_path / "routing.json"
    _write_json(path, _doc([], {
        "routes": {"Artists": True},
        "miscAutoMigrations": [{"artist": "A", "count": True}],
    }))
    s = routing.parse_batch_summary(path)
    assert s["routes"] == {}
    assert s["miscAutoMigrations"] == []


def test_parse_routing_document_returns_entries_and_summary_in_one_read(tmp_path):
    path = tmp_path / "routing.json"
    _write_json(path, _doc([{"filename": "Song.mp3", "compilationAlbum": True}],
                           {"routes": {"Compilations": 1}}))
    entries, summary = routing.parse_routing_document(path)
    assert [e["filename"] for e in entries] == ["Song.mp3"]
    assert entries[0]["compilationAlbum"] is True
    assert summary["routes"] == {"Compilations": 1}


def test_empty_summary_constant_is_not_shared_between_callers(tmp_path):
    """EMPTY_SUMMARY is a module-level dict - a caller mutating what it got
    back must not poison the next parse."""
    path = tmp_path / "routing.json"
    _write_json(path, [{"filename": "A.mp3"}])
    first = routing.parse_batch_summary(path)
    first["routes"]["Injected"] = 99
    assert routing.parse_batch_summary(path)["routes"] == {}


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

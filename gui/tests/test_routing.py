"""Unit tests for gui.routing - the exe's routing/execution JSON contract
(parse_routing_file, parse_execution_record) and the marker-line extraction
regexes that read the exe's stdout (routing_path_from_output,
execution_record_path). All three are pure and exe-output-format sensitive,
so a silent drift in any of them should fail here first."""
import json
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from gui import config, routing
from gui.data_loader import SchemaVersionError


# ------------------------------------------------------- parse_routing_file


def _write_json(path, data):
    path.write_text(json.dumps(data), encoding="utf-8")


def _doc(files=None, summary=None, **extra):
    """A minimal valid routing document - always carries schemaVersion,
    since every parser in this module now hard-fails without one."""
    d = {"schemaVersion": 1, "files": files if files is not None else []}
    if summary is not None:
        d["summary"] = summary
    d.update(extra)
    return d


def test_parse_routing_file_full_entry_round_trips_all_fields(tmp_path):
    path = tmp_path / "routing.json"
    _write_json(path, _doc([{
        "filename": "Song.mp3", "artist": "Artist", "title": "Title", "album": "Album",
        "destination": "Artists/Artist", "reason": "clean", "isNewFolder": True,
        "status": "ok", "detail": "", "inBatchDuplicate": True, "compilationAlbum": True,
        "tagChanges": ["title", "artist"],
        "libraryDuplicate": True, "dupLibraryPath": "Artists/Artist/Album/Song.mp3",
        "dupLibraryTrack": "Title", "dupLibraryAlbum": "Album", "dupNewAlbum": "Album (Deluxe)",
        "dupRecommendationKey": "L", "dupRecommendation": "Delete library copy",
        "dupReason": "deluxe preferred",
    }]))
    entries = routing.parse_routing_file(path)
    assert entries == [{
        "filename": "Song.mp3", "artist": "Artist", "title": "Title", "album": "Album",
        "destination": "Artists/Artist", "reason": "clean", "isNewFolder": True,
        "status": "ok", "detail": "", "inBatchDuplicate": True, "compilationAlbum": True,
        "tagChanges": ["title", "artist"],
        "libraryDuplicate": True, "dupLibraryPath": "Artists/Artist/Album/Song.mp3",
        "dupLibraryTrack": "Title", "dupLibraryAlbum": "Album", "dupNewAlbum": "Album (Deluxe)",
        "dupRecommendationKey": "L", "dupRecommendation": "Delete library copy",
        "dupReason": "deluxe preferred",
    }]


def test_parse_routing_file_missing_fields_default_to_safe_values(tmp_path):
    path = tmp_path / "routing.json"
    _write_json(path, _doc([{"filename": "Song.mp3"}]))
    entries = routing.parse_routing_file(path)
    assert entries == [{
        "filename": "Song.mp3", "artist": "", "title": "", "album": "",
        "destination": "", "reason": "", "isNewFolder": False,
        "status": "", "detail": "", "inBatchDuplicate": False, "compilationAlbum": False,
        "tagChanges": [],
        "libraryDuplicate": False, "dupLibraryPath": "", "dupLibraryTrack": "",
        "dupLibraryAlbum": "", "dupNewAlbum": "", "dupRecommendationKey": "",
        "dupRecommendation": "", "dupReason": "",
    }]


def test_parse_routing_file_drops_entries_missing_filename(tmp_path):
    path = tmp_path / "routing.json"
    _write_json(path, _doc([{"filename": "keep.mp3"}, {"artist": "no filename"}]))
    entries = routing.parse_routing_file(path)
    assert [e["filename"] for e in entries] == ["keep.mp3"]


def test_parse_routing_file_drops_non_dict_entries(tmp_path):
    path = tmp_path / "routing.json"
    _write_json(path, _doc([{"filename": "keep.mp3"}, "a string", 123, None]))
    entries = routing.parse_routing_file(path)
    assert [e["filename"] for e in entries] == ["keep.mp3"]


def test_parse_routing_file_missing_files_key_returns_empty(tmp_path):
    path = tmp_path / "routing.json"
    _write_json(path, {"schemaVersion": 1})
    assert routing.parse_routing_file(path) == []


def test_parse_routing_file_non_string_optional_fields_default_safely(tmp_path):
    """A malformed exe output (e.g. a JSON null instead of a string) must not
    crash the parser - it should fall back to the same default as a missing
    field, not propagate the wrong type into the GUI."""
    path = tmp_path / "routing.json"
    _write_json(path, _doc([{"filename": "Song.mp3", "artist": None, "tagChanges": [1, "title", None]}]))
    entries = routing.parse_routing_file(path)
    assert entries[0]["artist"] == ""
    assert entries[0]["tagChanges"] == ["title"]


def test_parse_routing_file_handles_utf8_bom(tmp_path):
    path = tmp_path / "routing.json"
    path.write_bytes(b"\xef\xbb\xbf" + json.dumps(_doc([{"filename": "Song.mp3"}])).encode("utf-8"))
    entries = routing.parse_routing_file(path)
    assert [e["filename"] for e in entries] == ["Song.mp3"]


# --------------------------------------------------------- schemaVersion


def test_parse_routing_file_missing_schema_version_raises(tmp_path):
    path = tmp_path / "routing.json"
    _write_json(path, {"files": [{"filename": "Song.mp3"}]})
    try:
        routing.parse_routing_file(path)
        assert False, "expected SchemaVersionError"
    except SchemaVersionError as e:
        assert e.found is None
        assert e.expected == 1


def test_parse_routing_file_mismatched_schema_version_raises(tmp_path):
    path = tmp_path / "routing.json"
    _write_json(path, {"schemaVersion": 2, "files": [{"filename": "Song.mp3"}]})
    try:
        routing.parse_routing_file(path)
        assert False, "expected SchemaVersionError"
    except SchemaVersionError as e:
        assert e.found == 2


def test_parse_routing_file_bare_array_now_raises_instead_of_being_accepted(tmp_path):
    """The pre-versioned bare-top-level-array shape used to be read directly
    as the file list. Once schemaVersion exists, an unversioned document is a
    hard failure, never a silent read of an old file - see contract design
    doc section 4.2 ("hard-fail on missing or mismatched schemaVersion")."""
    path = tmp_path / "routing.json"
    _write_json(path, [{"filename": "Old.mp3"}])
    try:
        routing.parse_routing_file(path)
        assert False, "expected SchemaVersionError"
    except SchemaVersionError:
        pass


# ------------------------------------------------ batch summary (both shapes)


def test_parse_routing_file_reads_files_from_the_summary_shape(tmp_path):
    path = tmp_path / "routing.json"
    _write_json(path, _doc([{"filename": "Song.mp3"}], {"routes": {"Artists": 1}}))
    assert [e["filename"] for e in routing.parse_routing_file(path)] == ["Song.mp3"]


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
    _write_json(path, _doc([{"filename": "A.mp3"}]))
    first = routing.parse_batch_summary(path)
    first["routes"]["Injected"] = 99
    assert routing.parse_batch_summary(path)["routes"] == {}


# --------------------------------------------------- routing_path_from_output


def test_routing_path_from_output_extracts_path_when_file_exists(tmp_path, monkeypatch):
    json_path = tmp_path / "routing-20260903-120000.json"
    json_path.write_text("{}", encoding="utf-8")
    lines = ["Some other output", f"  JSON: {json_path}", "Done"]
    assert routing.routing_path_from_output(lines, 0.0) == json_path


def test_routing_path_from_output_ignores_matched_path_that_does_not_exist(tmp_path, monkeypatch):
    """A matched 'JSON: <path>' line pointing at a file that isn't actually
    there must not be trusted - fall back to scanning LOGS_DIR instead of
    handing back a dead path."""
    monkeypatch.setattr(config, "LOGS_DIR", tmp_path)
    real = tmp_path / "routing-20260903-090000.json"
    real.write_text("{}", encoding="utf-8")
    missing = tmp_path / "routing-20260903-120000.json"
    lines = [f"  JSON: {missing}"]
    assert routing.routing_path_from_output(lines, 0.0) == real


def test_routing_path_from_output_falls_back_to_newest_in_logs_dir(tmp_path, monkeypatch):
    monkeypatch.setattr(config, "LOGS_DIR", tmp_path)
    older = tmp_path / "routing-20260901-000000.json"
    newer = tmp_path / "routing-20260902-000000.json"
    older.write_text("{}", encoding="utf-8")
    newer.write_text("{}", encoding="utf-8")
    import os
    os.utime(older, (time.time() - 100, time.time() - 100))
    assert routing.routing_path_from_output([], 0.0) == newer


def test_routing_path_from_output_returns_none_when_nothing_found(tmp_path, monkeypatch):
    monkeypatch.setattr(config, "LOGS_DIR", tmp_path)
    assert routing.routing_path_from_output(["no json line here"], 0.0) is None


def test_routing_path_from_output_ignores_files_older_than_since(tmp_path, monkeypatch):
    """A stale routing-*.json from an earlier run must never be picked up as
    this run's output - only the marker-line match is exempt from `since`,
    because it names the exact artifact this run wrote."""
    monkeypatch.setattr(config, "LOGS_DIR", tmp_path)
    stale = tmp_path / "routing-20260901-000000.json"
    stale.write_text("{}", encoding="utf-8")
    since = time.time() + 1000
    assert routing.routing_path_from_output([], since) is None


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


# --------------------------------------------------- parse_execution_record


def _execution_doc(files=None, confidence=None, record_type="execution", dry_run=False, **extra):
    d = {
        "schemaVersion": 1, "recordType": record_type, "dryRun": dry_run,
        "generatedAt": "2026-09-06T14:03:11",
        "summary": {"routes": {"Artists": 1}, "miscAutoMigrations": [], "miscAutoMigrationTotal": 0,
                    "compilationAlbums": []},
        "confidence": confidence,
        "files": files if files is not None else [],
    }
    d.update(extra)
    return d


def _clean_confidence():
    return {
        "countCheck": {"totalFiles": 12, "moved": 12, "skipped": 0, "expectedMoved": 12, "ok": True},
        "sanityCheck": {"ran": True, "ok": True, "checked": 12, "failures": []},
        "newFolders": ["Artists/Dave/Singles"],
        "errorCount": 0,
        "errors": [],
    }


def test_parse_execution_record_valid_fixture_returns_full_shape(tmp_path):
    path = tmp_path / "execution.json"
    _write_json(path, _execution_doc(
        files=[{"filename": "Dave - Titanium.mp3", "artist": "Dave", "status": "moved", "detail": ""}],
        confidence=_clean_confidence(),
    ))
    record = routing.parse_execution_record(path)
    assert record["generatedAt"] == "2026-09-06T14:03:11"
    assert record["summary"]["routes"] == {"Artists": 1}
    assert record["files"][0]["filename"] == "Dave - Titanium.mp3"
    assert record["files"][0]["detail"] == ""
    c = record["confidence"]
    assert c["count_ok"] is True
    assert c["sanity_ran"] is True
    assert c["sanity_ok"] is True
    assert c["sanity_checked"] == 12
    assert c["sanity_failures"] == []
    assert c["new_folders"] == ["Artists/Dave/Singles"]
    assert c["error_count"] == 0
    assert c["errors"] == []
    assert c["total_count"] == 12
    assert c["moved_count"] == 12
    assert c["skipped_count"] == 0


def test_parse_execution_record_detail_field_present(tmp_path):
    path = tmp_path / "execution.json"
    _write_json(path, _execution_doc(
        files=[{"filename": "Bad.mp3", "status": "error", "detail": "Access denied"}],
        confidence=None,
    ))
    record = routing.parse_execution_record(path)
    assert record["files"][0]["detail"] == "Access denied"


def test_parse_execution_record_missing_schema_version_raises(tmp_path):
    path = tmp_path / "execution.json"
    d = _execution_doc()
    del d["schemaVersion"]
    _write_json(path, d)
    try:
        routing.parse_execution_record(path)
        assert False, "expected SchemaVersionError"
    except SchemaVersionError as e:
        assert e.found is None


def test_parse_execution_record_wrong_schema_version_raises(tmp_path):
    path = tmp_path / "execution.json"
    d = _execution_doc()
    d["schemaVersion"] = 2
    _write_json(path, d)
    try:
        routing.parse_execution_record(path)
        assert False, "expected SchemaVersionError"
    except SchemaVersionError as e:
        assert e.found == 2


def test_parse_execution_record_bare_array_raises(tmp_path):
    path = tmp_path / "execution.json"
    _write_json(path, [{"filename": "Old.mp3"}])
    try:
        routing.parse_execution_record(path)
        assert False, "expected SchemaVersionError"
    except SchemaVersionError:
        pass


def test_parse_execution_record_routing_record_type_raises(tmp_path):
    """A dry-run document must never be interpretable as a real outcome, even
    if someone points execution_record_path at logs/routing-*.json by
    mistake - recordType/dryRun are checked independently of the filename."""
    path = tmp_path / "routing.json"
    _write_json(path, _execution_doc(record_type="routing", dry_run=True, confidence=None))
    try:
        routing.parse_execution_record(path)
        assert False, "expected SchemaVersionError"
    except SchemaVersionError:
        pass


def test_parse_execution_record_dry_run_true_raises_even_with_execution_type(tmp_path):
    """Belt-and-braces: recordType and dryRun are both checked, not just
    whichever one a hand-edited or malformed file happens to get right."""
    path = tmp_path / "execution.json"
    _write_json(path, _execution_doc(record_type="execution", dry_run=True, confidence=None))
    try:
        routing.parse_execution_record(path)
        assert False, "expected SchemaVersionError"
    except SchemaVersionError:
        pass


def test_parse_execution_record_malformed_confidence_yields_full_defaulted_key_set(tmp_path):
    """A string where an int is expected, or a null array, must default
    rather than raise - the same defensive discipline as _parse_summary."""
    path = tmp_path / "execution.json"
    _write_json(path, _execution_doc(
        files=[],
        confidence={
            "countCheck": {"totalFiles": "many", "moved": None, "skipped": 0,
                           "expectedMoved": 0, "ok": "yes"},
            "sanityCheck": {"ran": True, "ok": True, "checked": "lots", "failures": None},
            "newFolders": None,
            "errorCount": "bad",
            "errors": None,
        },
    ))
    record = routing.parse_execution_record(path)
    c = record["confidence"]
    assert c["total_count"] is None
    assert c["moved_count"] is None
    assert c["skipped_count"] == 0
    assert c["sanity_checked"] == 0
    assert c["sanity_failures"] == []
    assert c["new_folders"] == []
    assert c["error_count"] == 0
    assert c["errors"] == []


def test_parse_execution_record_reads_the_checked_in_fixture():
    """A real-shaped fixture on disk (gui/tests/fixtures/execution-sample.json),
    not just an inline dict built by this test file - the same discipline
    other fixture-backed parsers in this repo already follow."""
    path = Path(__file__).resolve().parent / "fixtures" / "execution-sample.json"
    record = routing.parse_execution_record(path)
    assert [f["filename"] for f in record["files"]] == ["Dave - Titanium.mp3", "Dave - Runaway.mp3"]
    assert record["confidence"]["moved_count"] == 2
    assert record["confidence"]["new_folders"] == ["Artists\\Dave\\Singles"]


def test_parse_execution_record_null_confidence_on_dry_run_shape_is_none(tmp_path):
    path = tmp_path / "execution.json"
    _write_json(path, _execution_doc(files=[], confidence=None))
    record = routing.parse_execution_record(path)
    assert record["confidence"] is None


# ----------------------------------------------------- execution_record_path


def test_execution_record_path_extracts_path_when_file_exists(tmp_path):
    json_path = tmp_path / "execution-20260906-140311.json"
    json_path.write_text("{}", encoding="utf-8")
    lines = ["Some other output", f"  EXECUTION JSON: {json_path}", "Done"]
    assert routing.execution_record_path(lines, 0.0) == json_path


def test_execution_record_path_falls_back_to_newest_qualifying_file(tmp_path, monkeypatch):
    monkeypatch.setattr(config, "LOGS_DIR", tmp_path)
    older = tmp_path / "execution-20260901-000000.json"
    newer = tmp_path / "execution-20260902-000000.json"
    older.write_text("{}", encoding="utf-8")
    newer.write_text("{}", encoding="utf-8")
    import os
    os.utime(older, (time.time() - 100, time.time() - 100))
    assert routing.execution_record_path([], 0.0) == newer


def test_execution_record_path_returns_none_when_only_candidate_predates_since(tmp_path, monkeypatch):
    monkeypatch.setattr(config, "LOGS_DIR", tmp_path)
    stale = tmp_path / "execution-20260901-000000.json"
    stale.write_text("{}", encoding="utf-8")
    since = time.time() + 1000
    assert routing.execution_record_path([], since) is None


# ------------------------------------------------------------- newmusic_path


def test_newmusic_path_joins_newmusic_dir(monkeypatch, tmp_path):
    monkeypatch.setattr(config, "NEWMUSIC_DIR", tmp_path)
    assert routing.newmusic_path("Artist - Song.mp3") == tmp_path / "Artist - Song.mp3"


def test_newmusic_path_preserves_relative_subfolders(monkeypatch, tmp_path):
    monkeypatch.setattr(config, "NEWMUSIC_DIR", tmp_path)
    assert routing.newmusic_path("Sub/Song.mp3") == tmp_path / "Sub" / "Song.mp3"

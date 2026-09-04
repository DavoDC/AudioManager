"""Unit tests for gui.tabs.integration - pure logic only (IntegrationState,
_esc, _update_exec_status). UI-building functions (stage_scan/review_card/etc.)
need a NiceGUI page context and are exercised manually per gui/README.md."""
import asyncio
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from gui import config
from gui.runner import RunResult
from gui.tabs.integration import (
    IntegrationState, _bulk, _confirm_note_class, _esc, _failed_filename_from_output,
    _on_review_key, _open_run_log, _review_key_action, _sample_entries,
    _update_exec_status, _write_manifest, batch_summary_html, run_execute,
    run_execute_simulated, run_simulate, sort_by_destination,
)


def _entry(filename, **overrides):
    e = {
        "filename": filename, "artist": "Artist", "title": "Title", "album": "Album",
        "destination": "Artists/Artist", "reason": "", "isNewFolder": False,
        "status": "ok", "inBatchDuplicate": False, "compilationAlbum": False, "tagChanges": [],
        "libraryDuplicate": False, "dupLibraryPath": "", "dupLibraryTrack": "",
        "dupLibraryAlbum": "", "dupNewAlbum": "", "dupRecommendationKey": "",
        "dupRecommendation": "", "dupReason": "",
    }
    e.update(overrides)
    return e


# ------------------------------------------------------------- IntegrationState


def test_accepted_defaults_to_true_when_no_decision_recorded():
    s = IntegrationState()
    s.entries = [_entry("a.mp3")]
    assert s.accepted == [s.entries[0]]
    assert s.declined == []


def test_accepted_and_declined_split_on_decisions():
    s = IntegrationState()
    s.entries = [_entry("a.mp3"), _entry("b.mp3")]
    s.decisions = {"a.mp3": True, "b.mp3": False}
    assert [e["filename"] for e in s.accepted] == ["a.mp3"]
    assert [e["filename"] for e in s.declined] == ["b.mp3"]


def test_filtered_all_returns_every_entry():
    s = IntegrationState()
    s.entries = [_entry("a.mp3"), _entry("b.mp3")]
    s.filter = "all"
    assert s.filtered() == s.entries


def test_filtered_newfolders_only():
    s = IntegrationState()
    s.entries = [_entry("a.mp3", isNewFolder=True), _entry("b.mp3", isNewFolder=False)]
    s.filter = "newfolders"
    assert [e["filename"] for e in s.filtered()] == ["a.mp3"]


def test_filtered_conflicts_includes_dupes_and_non_clean_status():
    s = IntegrationState()
    s.entries = [
        _entry("dupe.mp3", inBatchDuplicate=True),
        _entry("err.mp3", status="error"),
        _entry("clean.mp3", status="ok"),
    ]
    s.filter = "conflicts"
    names = {e["filename"] for e in s.filtered()}
    assert names == {"dupe.mp3", "err.mp3"}


def test_filtered_sorts_by_destination_grouping_same_album_adjacent():
    """The GUI must mirror the CLI's own destination-path sort
    (MusicIntegrator.cs ~188-192) so same-album files land next to each
    other, instead of the exe's raw scan order."""
    s = IntegrationState()
    s.entries = [
        _entry("z.mp3", destination="Artists/B Artist/Album Two"),
        _entry("a.mp3", destination="Artists/A Artist/Album One"),
        _entry("b.mp3", destination="Artists/A Artist/Album One"),
    ]
    s.filter = "all"
    assert [e["filename"] for e in s.filtered()] == ["a.mp3", "b.mp3", "z.mp3"]


def test_sort_by_destination_ties_break_on_filename():
    entries = [_entry("b.mp3", destination="X"), _entry("a.mp3", destination="X")]
    assert [e["filename"] for e in sort_by_destination(entries)] == ["a.mp3", "b.mp3"]


def test_sort_by_destination_missing_destination_sorts_first():
    entries = [_entry("has-dest.mp3", destination="Artists/A"), _entry("no-dest.mp3", destination="")]
    assert [e["filename"] for e in sort_by_destination(entries)] == ["no-dest.mp3", "has-dest.mp3"]


def test_filtered_libdupes_is_distinct_from_inbatch_duplicate():
    """"In-batch duplicate" (same artist+title twice in this scan) and
    "Library duplicate" (already exists in the library) are two unrelated
    concepts - the libdupes filter must key off libraryDuplicate only, never
    inBatchDuplicate."""
    s = IntegrationState()
    s.entries = [
        _entry("inbatch.mp3", inBatchDuplicate=True, libraryDuplicate=False),
        _entry("libdupe.mp3", inBatchDuplicate=False, libraryDuplicate=True),
        _entry("clean.mp3"),
    ]
    s.filter = "libdupes"
    assert [e["filename"] for e in s.filtered()] == ["libdupe.mp3"]


# ------------------------------------------------------------ dup_resolution


def test_dup_resolution_defaults_to_exe_recommendation_when_untouched():
    """Leaving a library-duplicate entry untouched must resolve to the exe's
    own recommendation - the same value a real (unsupervised) run would take,
    per IDEAS.md's 'unresolved default = auto-take-recommendation' rule."""
    s = IntegrationState()
    e = _entry("a.mp3", libraryDuplicate=True, dupRecommendationKey="L")
    assert s.dup_resolution(e) == "L"


def test_dup_resolution_falls_back_to_keep_both_when_no_recommendation():
    s = IntegrationState()
    e = _entry("a.mp3", libraryDuplicate=True, dupRecommendationKey="")
    assert s.dup_resolution(e) == "K"


def test_dup_resolution_explicit_override_beats_recommendation():
    s = IntegrationState()
    e = _entry("a.mp3", libraryDuplicate=True, dupRecommendationKey="L")
    s.dup_resolutions["a.mp3"] = "D"
    assert s.dup_resolution(e) == "D"


def test_bulk_accept_only_touches_active_filter_not_all_entries():
    """_bulk must respect S.filtered() - looping S.entries unconditionally
    would silently accept/decline files hidden by the active filter."""
    s = IntegrationState()
    s.entries = [
        _entry("new.mp3", isNewFolder=True),
        _entry("other.mp3", isNewFolder=False),
    ]
    s.decisions = {"new.mp3": True, "other.mp3": True}
    s.filter = "newfolders"
    _with_state(s, lambda: _bulk(False))
    assert s.decisions["new.mp3"] is False
    assert s.decisions["other.mp3"] is True


def test_bulk_accept_all_filter_touches_every_entry():
    s = IntegrationState()
    s.entries = [_entry("a.mp3"), _entry("b.mp3")]
    s.decisions = {"a.mp3": False, "b.mp3": False}
    s.filter = "all"
    _with_state(s, lambda: _bulk(True))
    assert s.decisions["a.mp3"] is True
    assert s.decisions["b.mp3"] is True


# --------------------------------------------------------- _write_manifest


def test_write_manifest_passes_manifest_flag_with_zero_declines(tmp_path, monkeypatch):
    """A zero-declines run (accept-everything, the common case) must still
    write and pass --manifest - otherwise the exe re-scans NEWMUSIC_DIR from
    scratch and integrates anything that arrived after the dry run unreviewed."""
    monkeypatch.setattr(config, "CACHE_DIR", tmp_path)
    targets = [_entry("a.mp3"), _entry("b.mp3")]
    args = _write_manifest(targets)
    assert "--manifest" in args
    manifest_path = Path(args[args.index("--manifest") + 1])
    assert manifest_path.exists()
    written = json.loads(manifest_path.read_text(encoding="utf-8"))
    assert [e["filename"] for e in written] == ["a.mp3", "b.mp3"]


def test_write_manifest_includes_dup_resolution_for_library_duplicates_only(tmp_path, monkeypatch):
    """The guarded manifest write must carry the resolved D/L/K decision for a
    library-duplicate entry (defaulting to the recommendation via
    IntegrationState.dup_resolution - same code path as the unresolved
    fallback), and must NOT add a dupResolution key for a non-duplicate entry -
    the schema for ordinary entries stays exactly as before."""
    monkeypatch.setattr(config, "CACHE_DIR", tmp_path)
    s = IntegrationState()
    dup_entry = _entry("dupe.mp3", libraryDuplicate=True, dupRecommendationKey="L")
    plain_entry = _entry("plain.mp3")
    _with_state(s, lambda: _write_manifest([dup_entry, plain_entry]))

    written = json.loads((tmp_path / "accepted-manifest.json").read_text(encoding="utf-8"))
    by_name = {e["filename"]: e for e in written}
    assert by_name["dupe.mp3"]["dupResolution"] == "L"
    assert "dupResolution" not in by_name["plain.mp3"]


def test_write_manifest_uses_explicit_dup_resolution_override(tmp_path, monkeypatch):
    monkeypatch.setattr(config, "CACHE_DIR", tmp_path)
    s = IntegrationState()
    s.dup_resolutions["dupe.mp3"] = "D"
    dup_entry = _entry("dupe.mp3", libraryDuplicate=True, dupRecommendationKey="L")
    _with_state(s, lambda: _write_manifest([dup_entry]))

    written = json.loads((tmp_path / "accepted-manifest.json").read_text(encoding="utf-8"))
    assert written[0]["dupResolution"] == "D"


def test_run_execute_writes_manifest_excluding_declined_tracks(tmp_path, monkeypatch):
    """run_execute must build the manifest from S.accepted, not S.entries -
    a declined track reaching the manifest means the exe would move a file
    the user explicitly told the GUI to leave in NewMusic."""
    monkeypatch.setattr(config, "CACHE_DIR", tmp_path)
    monkeypatch.setattr(config, "RUN_LOGS_DIR", tmp_path / "run-logs")

    import gui.tabs.integration as integration_module

    state = IntegrationState()
    state.entries = [_entry("keep.mp3"), _entry("drop.mp3")]
    state.decisions = {"keep.mp3": True, "drop.mp3": False}

    async def fake_run(args, action="", on_line=None, timeout=None):
        return RunResult(command=args, returncode=0, lines=[])

    monkeypatch.setattr(integration_module.runner, "run", fake_run)

    original = integration_module.S
    integration_module.S = state
    try:
        asyncio.run(run_execute())
    finally:
        integration_module.S = original

    manifest_path = tmp_path / "accepted-manifest.json"
    written = json.loads(manifest_path.read_text(encoding="utf-8"))
    assert [e["filename"] for e in written] == ["keep.mp3"]


# -------------------------------------------------- _failed_filename_from_output


def test_failed_filename_from_output_extracts_name_after_prefix():
    lines = ["[AUTO] Artist - Title", "Error processing file: Song.mp3", "INTEGRATION FAILED"]
    assert _failed_filename_from_output(lines) == "Song.mp3"


def test_failed_filename_from_output_returns_none_when_absent():
    assert _failed_filename_from_output(["[AUTO] Artist - Title", "INTEGRATION FAILED"]) is None


def test_run_execute_on_exe_failure_marks_named_file_failed_others_notrun(tmp_path, monkeypatch):
    """The exe processes targets one at a time and halts on its first error -
    only the file it names in 'Error processing file: <name>' was actually
    attempted; everything still queued/moving after that is unattempted, not
    failed, and must be labelled accordingly rather than lumped in as failed."""
    monkeypatch.setattr(config, "CACHE_DIR", tmp_path)
    monkeypatch.setattr(config, "RUN_LOGS_DIR", tmp_path / "run-logs")

    import gui.tabs.integration as integration_module

    state = IntegrationState()
    state.entries = [_entry("a.mp3"), _entry("b.mp3"), _entry("c.mp3")]

    async def fake_run(args, action="", on_line=None, timeout=None):
        return RunResult(command=args, returncode=1,
                          lines=["[AUTO] Artist - A", "Error processing file: b.mp3", "INTEGRATION FAILED"])

    monkeypatch.setattr(integration_module.runner, "run", fake_run)

    original = integration_module.S
    integration_module.S = state
    try:
        asyncio.run(run_execute())
    finally:
        integration_module.S = original

    assert state.exec_status["a.mp3"] == "notrun"
    assert state.exec_status["b.mp3"] == "failed"
    assert state.exec_status["c.mp3"] == "notrun"
    assert "b.mp3" in state.exec_summary
    assert "2 file(s) were not attempted" in state.exec_summary
    # Regression: the old code appended "1 file failed: b.mp3." after a
    # summary that already ends in "...Error processing file: b.mp3" with no
    # separator, producing a duplicated, run-on filename mention.
    assert state.exec_summary.count("b.mp3") == 1


def test_run_execute_on_failure_refreshes_before_opening_error_modal(tmp_path, monkeypatch):
    """S.refresh() rebuilds the @ui.refreshable slot _finish_execute runs
    inside - opening the error modal before that refresh gets it destroyed
    the instant the rebuild happens. The modal must open AFTER refresh."""
    monkeypatch.setattr(config, "CACHE_DIR", tmp_path)
    monkeypatch.setattr(config, "RUN_LOGS_DIR", tmp_path / "run-logs")

    import gui.tabs.integration as integration_module

    call_order = []
    monkeypatch.setattr(integration_module, "show_error_modal",
                         lambda *a, **k: call_order.append("modal"))

    state = IntegrationState()
    state.entries = [_entry("a.mp3")]
    state.refresh = lambda: call_order.append("refresh")

    async def fake_run(args, action="", on_line=None, timeout=None):
        return RunResult(command=args, returncode=1,
                          lines=["Error processing file: a.mp3", "INTEGRATION FAILED"])

    monkeypatch.setattr(integration_module.runner, "run", fake_run)

    original = integration_module.S
    integration_module.S = state
    try:
        asyncio.run(run_execute())
    finally:
        integration_module.S = original

    # run_execute() also refreshes once before the exe call - only the
    # relative order around the modal matters here.
    assert call_order[-1] == "modal"
    assert call_order[-2] == "refresh"


def test_finish_execute_sets_exec_ok_true_on_success():
    import gui.tabs.integration as integration_module
    state = IntegrationState()
    state.exec_targets = [_entry("a.mp3")]
    state.exec_status = {"a.mp3": "queued"}
    _with_state(state, lambda: integration_module._finish_execute(
        RunResult(command=["integrate"], returncode=0, lines=[])))
    assert state.exec_ok is True


def test_finish_execute_sets_exec_ok_false_on_failure(monkeypatch):
    import gui.tabs.integration as integration_module
    monkeypatch.setattr(integration_module, "show_error_modal", lambda *a, **k: None)
    state = IntegrationState()
    state.exec_targets = [_entry("a.mp3")]
    state.exec_status = {"a.mp3": "queued"}
    _with_state(state, lambda: integration_module._finish_execute(
        RunResult(command=["integrate"], returncode=1, lines=["INTEGRATION FAILED"])))
    assert state.exec_ok is False


def test_run_execute_on_cancel_marks_everything_notrun_not_failed(tmp_path, monkeypatch):
    monkeypatch.setattr(config, "CACHE_DIR", tmp_path)
    monkeypatch.setattr(config, "RUN_LOGS_DIR", tmp_path / "run-logs")

    import gui.tabs.integration as integration_module

    state = IntegrationState()
    state.entries = [_entry("a.mp3")]

    async def fake_run(args, action="", on_line=None, timeout=None):
        return RunResult(command=args, returncode=None, cancelled=True, lines=[])

    monkeypatch.setattr(integration_module.runner, "run", fake_run)

    original = integration_module.S
    integration_module.S = state
    try:
        asyncio.run(run_execute())
    finally:
        integration_module.S = original

    assert state.exec_status["a.mp3"] == "notrun"


# --------------------------------------------------------------- _open_run_log


def test_open_run_log_creates_timestamped_file_under_run_logs_dir(tmp_path, monkeypatch):
    monkeypatch.setattr(config, "RUN_LOGS_DIR", tmp_path / "run-logs")
    f = _open_run_log()
    try:
        assert f is not None
        f.write("hello\n")
        f.flush()
        logged = list((tmp_path / "run-logs").glob("integration-*.log"))
        assert len(logged) == 1
        assert logged[0].read_text(encoding="utf-8") == "hello\n"
    finally:
        if f:
            f.close()


# ---------------------------------------------------------- _confirm_note_class


def test_confirm_note_class_real_run_gets_warning_weight_not_plain_note():
    """The real-run confirm note must never fall back to plain dim `.note` -
    it is the single highest-stakes confirmation in the GUI, so it needs at
    least the same loud/highlighted treatment as the simulated note."""
    assert _confirm_note_class(False) == "note real"
    assert _confirm_note_class(False) != "note"


def test_confirm_note_class_simulated_still_gets_its_own_loud_class():
    assert _confirm_note_class(True) == "note simulated"


# --------------------------------------------------------------------- _esc


def test_esc_escapes_html_special_chars():
    assert _esc("<b>Rock & Roll</b>") == "&lt;b&gt;Rock &amp; Roll&lt;/b&gt;"


def test_esc_escapes_quotes():
    assert "&quot;" in _esc('say "hi"')
    assert "&#x27;" in _esc("it's")


# ---------------------------------------------------------- _update_exec_status


def test_update_exec_status_marks_auto_line_done():
    """The real exe's success line is `[AUTO] {Artist} - {Title}` - no
    filename at all, only tag text."""
    s = IntegrationState()
    s.exec_targets = [_entry("Song.mp3", artist="Some Artist", title="Some Title")]
    s.exec_status = {"Song.mp3": "queued"}
    _update_exec_status_on(s, "[AUTO] Some Artist - Some Title")
    assert s.exec_status["Song.mp3"] == "done"


def test_update_exec_status_marks_skip_line_done():
    """`[SKIP]` means the exe deliberately left the file in place (e.g.
    already exists at destination) - not a failure, so it also settles to
    'done' rather than 'failed'."""
    s = IntegrationState()
    s.exec_targets = [_entry("Song.mp3", artist="Some Artist", title="Some Title")]
    s.exec_status = {"Song.mp3": "queued"}
    _update_exec_status_on(s, "[SKIP] Some Artist - Some Title: already exists at destination")
    assert s.exec_status["Song.mp3"] == "done"


def test_update_exec_status_marks_named_file_failed_on_error_line():
    """The halt-on-error line is the one real-exe line that DOES carry a
    filename: `Error processing file: {filename}`."""
    s = IntegrationState()
    s.exec_targets = [_entry("Song.mp3")]
    s.exec_status = {"Song.mp3": "queued"}
    _update_exec_status_on(s, "Error processing file: Song.mp3")
    assert s.exec_status["Song.mp3"] == "failed"


def test_update_exec_status_overlapping_tag_text_does_not_cross_contaminate():
    """A shorter accepted track's "artist - title" text that is a substring of
    another track's must NOT have its status flipped by a line meant for the
    other file: _update_exec_status ignores a substring match when a longer
    target's tag text also matches the same line."""
    s = IntegrationState()
    s.exec_targets = [
        _entry("Song.mp3", artist="Artist", title="Song"),
        _entry("Another Song.mp3", artist="Artist", title="Another Song"),
    ]
    s.exec_status = {"Song.mp3": "queued", "Another Song.mp3": "queued"}
    _update_exec_status_on(s, "[AUTO] Artist - Another Song")
    assert s.exec_status["Another Song.mp3"] == "done"
    assert s.exec_status["Song.mp3"] == "queued"  # untouched: not the file this line is about


# ------------------------------------------------------------- Simulate mode


def _with_state(state, fn):
    """Swap in `state` as the module-level S singleton for the duration of `fn`."""
    import gui.tabs.integration as integration_module
    original = integration_module.S
    integration_module.S = state
    try:
        fn()
    finally:
        integration_module.S = original


# ------------------------------------------------- batch scan-ahead summary


def _summary(**overrides):
    s = {"routes": {}, "miscAutoMigrations": [], "miscAutoMigrationTotal": 0,
         "compilationAlbums": []}
    s.update(overrides)
    return s


def test_batch_summary_is_silent_when_there_is_no_batch_context():
    """No routes, no migrations, no compilations - render nothing rather than
    an empty strip that means "nothing here"."""
    assert batch_summary_html(_summary()) == ""


def test_batch_summary_lists_routes_highest_count_first():
    html = batch_summary_html(_summary(routes={"Singles": 4, "Artists": 12, "Compilations": 2}))
    assert "Artists" in html and "Singles" in html and "Compilations" in html
    assert html.index("Artists") < html.index("Singles") < html.index("Compilations")


def test_batch_summary_route_distribution_alone_is_enough_to_render():
    assert batch_summary_html(_summary(routes={"Artists": 3})) != ""


def test_batch_summary_announces_misc_automigration_as_existing_library_songs():
    """The highest-stakes line: it says the run moves files ALREADY in the
    library, not just incoming ones. The wording must carry that."""
    html = batch_summary_html(_summary(
        miscAutoMigrations=[{"artist": "Hopsin", "count": 3}], miscAutoMigrationTotal=3))
    assert "existing library song(s)" in html
    assert "Hopsin (3)" in html
    assert "3 existing" in html


def test_batch_summary_recomputes_migration_total_when_absent():
    html = batch_summary_html(_summary(
        miscAutoMigrations=[{"artist": "A", "count": 2}, {"artist": "B", "count": 3}]))
    assert "5 existing library song(s)" in html


def test_batch_summary_truncates_a_long_migration_list():
    migrations = [{"artist": f"Artist{i}", "count": 1} for i in range(9)]
    html = batch_summary_html(_summary(miscAutoMigrations=migrations))
    assert "+3 more" in html
    assert "Artist8" not in html


def test_batch_summary_names_detected_compilation_albums():
    html = batch_summary_html(_summary(compilationAlbums=["Now 42", "Trance Nation"]))
    assert "Now 42" in html and "Trance Nation" in html
    assert "Compilation album(s) detected" in html


def test_batch_summary_truncates_a_long_compilation_list():
    html = batch_summary_html(_summary(compilationAlbums=[f"Album {i}" for i in range(7)]))
    assert "+3 more" in html


def test_batch_summary_escapes_html_in_artist_and_album_names():
    html = batch_summary_html(_summary(
        routes={"<b>Artists</b>": 1},
        miscAutoMigrations=[{"artist": "A&B <script>", "count": 1}],
        compilationAlbums=["<img src=x>"]))
    assert "<script>" not in html
    assert "<img src=x>" not in html
    assert "&lt;b&gt;Artists&lt;/b&gt;" in html


def test_batch_summary_tolerates_a_summary_with_missing_keys():
    """Defensive: a summary from an older exe build (or a partial parse) must
    not raise on a key the renderer expects."""
    assert batch_summary_html({}) == ""


def test_filtered_compilations_selects_only_compilation_album_entries():
    s = IntegrationState()
    s.entries = [_entry("a.mp3", compilationAlbum=True), _entry("b.mp3"),
                 _entry("c.mp3", compilationAlbum=True)]
    s.filter = "compilations"
    assert [e["filename"] for e in s.filtered()] == ["a.mp3", "c.mp3"]


def test_compilation_album_is_independent_of_the_other_card_signals():
    """A compilation-album file is not a duplicate and not a new folder - the
    filters must not alias, or the badge stops meaning anything."""
    s = IntegrationState()
    s.entries = [_entry("comp.mp3", compilationAlbum=True)]
    s.filter = "conflicts"
    assert s.filtered() == []
    s.filter = "newfolders"
    assert s.filtered() == []
    s.filter = "libdupes"
    assert s.filtered() == []


def test_sample_entries_include_a_compilation_album_card():
    entries = _sample_entries()
    comp = [e for e in entries if e.get("compilationAlbum")]
    assert len(comp) == 1
    assert comp[0]["destination"].startswith("Compilations/")
    assert not comp[0]["libraryDuplicate"] and not comp[0]["inBatchDuplicate"]


def test_run_simulate_loads_batch_summary_context(monkeypatch):
    import gui.tabs.integration as integration_module
    monkeypatch.setattr(integration_module.runner, "run", lambda *a, **k: None)

    state = IntegrationState()
    _with_state(state, run_simulate)

    assert state.summary["routes"]
    assert state.summary["miscAutoMigrations"]
    assert state.summary["compilationAlbums"]
    assert batch_summary_html(state.summary) != ""


def test_sample_entries_cover_every_review_card_state():
    entries = _sample_entries()
    assert any(e["isNewFolder"] for e in entries)
    assert any(e["inBatchDuplicate"] for e in entries)
    assert any(e["libraryDuplicate"] for e in entries)
    assert any(e["tagChanges"] for e in entries)
    assert any(e["status"] == "error" for e in entries)
    assert any(not e["isNewFolder"] and not e["inBatchDuplicate"]
               and not e["libraryDuplicate"] and not e["tagChanges"]
               and e["status"] == "ok" for e in entries)


def test_sample_entries_library_duplicate_carries_full_cli_parity_info():
    """The library-duplicate sample must carry every field the review card's
    resolution control needs to show full CLI parity (library copy's path/
    track/album, the new file's album, recommendation, and reason) - not just
    the boolean flag."""
    entries = _sample_entries()
    e = next(e for e in entries if e["libraryDuplicate"])
    assert e["dupLibraryPath"]
    assert e["dupLibraryTrack"]
    assert e["dupLibraryAlbum"]
    assert e["dupNewAlbum"]
    assert e["dupRecommendationKey"] in ("D", "L", "K")
    assert e["dupRecommendation"]
    assert e["dupReason"]


def test_run_simulate_loads_sample_data_without_touching_runner(monkeypatch):
    import gui.tabs.integration as integration_module

    def fail_if_called(*a, **k):
        raise AssertionError("run_simulate must never call the real exe runner")
    monkeypatch.setattr(integration_module.runner, "run", fail_if_called)

    state = IntegrationState()
    _with_state(state, run_simulate)

    assert state.simulated is True
    assert state.stage == 2
    assert state.entries == _sample_entries()
    assert all(v is True for v in state.decisions.values())


def test_run_scan_resets_simulated_flag(monkeypatch):
    """A real scan must clear any leftover simulated flag from a prior Simulate run."""
    import gui.tabs.integration as integration_module
    state = IntegrationState()
    state.simulated = True

    async def fake_run(args, action="", on_line=None, timeout=None):
        return RunResult(command=args, returncode=1, lines=[])  # fails fast, no further state changes needed

    monkeypatch.setattr(integration_module.runner, "run", fake_run)
    monkeypatch.setattr(integration_module, "show_error_modal", lambda *a, **k: None)

    original = integration_module.S
    integration_module.S = state
    try:
        asyncio.run(integration_module.run_scan())
    finally:
        integration_module.S = original

    assert state.simulated is False


def test_run_execute_simulated_never_calls_real_runner(monkeypatch):
    import gui.tabs.integration as integration_module

    def fail_if_called(*a, **k):
        raise AssertionError("run_execute_simulated must never call the real exe runner")
    monkeypatch.setattr(integration_module.runner, "run", fail_if_called)

    state = IntegrationState()
    state.entries = [_entry("a.mp3"), _entry("b.mp3")]
    state.simulated = True

    _with_state(state, lambda: asyncio.run(run_execute_simulated()))

    assert state.exec_done is True
    assert state.exec_status["a.mp3"] == "done"
    assert state.exec_status["b.mp3"] == "done"


def test_run_execute_simulated_never_includes_the_error_sample_and_never_fails(monkeypatch):
    """One sample entry has status 'error' - IntegrationState.accepted excludes
    error-status entries unconditionally (2026-09-04: errored items must never
    be accepted, so a single unresolved file can no longer abort the rest of
    the batch), so run_execute_simulated's targets never include it and the
    simulated run always succeeds with every accepted entry marked done."""
    import gui.tabs.integration as integration_module
    monkeypatch.setattr(integration_module, "show_error_modal", lambda *a, **k: None)

    state = IntegrationState()
    state.entries = _sample_entries()

    _with_state(state, lambda: asyncio.run(run_execute_simulated()))

    samples = _sample_entries()
    error_entry = next(e for e in samples if e["status"] == "error")
    assert error_entry["filename"] not in state.exec_status
    assert state.exec_ok is True
    assert not any(v == "notrun" for v in state.exec_status.values())
    assert "not attempted" not in state.exec_summary

    # Regression: every accepted entry got a real `[AUTO]` success line and
    # must show "done" - this is the bug the 2026-09-03 Opus review found
    # (_update_exec_status never matched `[AUTO]` lines, so successful files
    # were mislabelled not-run).
    for e in samples:
        if e["status"] != "error":
            assert state.exec_status[e["filename"]] == "done"


# ---------------------------------------------------- review-stage keyboard


class _FakeAction:
    def __init__(self, keydown=True, repeat=False):
        self.keydown = keydown
        self.repeat = repeat


class _FakeKey:
    def __init__(self, name):
        self.name = name


class _FakeKeyEvent:
    def __init__(self, key_name, keydown=True, repeat=False):
        self.action = _FakeAction(keydown=keydown, repeat=repeat)
        self.key = _FakeKey(key_name)


def test_review_key_action_maps_letters_and_arrows():
    assert _review_key_action("a") == "accept"
    assert _review_key_action("D") == "decline"
    assert _review_key_action("j") == "next"
    assert _review_key_action("ArrowDown") == "next"
    assert _review_key_action("k") == "prev"
    assert _review_key_action("ArrowUp") == "prev"
    assert _review_key_action("x") is None
    assert _review_key_action("") is None


def test_on_review_key_ignored_outside_review_stage():
    """The keyboard element is only mounted while stage_review() renders, but
    _on_review_key also self-guards on S.stage so a stray leftover handler
    (e.g. from a slow rebuild) can never accept/decline off-stage."""
    s = IntegrationState()
    s.stage = 1
    s.entries = [_entry("a.mp3")]
    s.decisions = {"a.mp3": True}
    _with_state(s, lambda: _on_review_key(_FakeKeyEvent("d")))
    assert s.decisions["a.mp3"] is True


def test_on_review_key_decline_targets_entry_under_cursor():
    s = IntegrationState()
    s.stage = 2
    s.entries = [_entry("a.mp3"), _entry("b.mp3")]
    s.decisions = {"a.mp3": True, "b.mp3": True}
    s.cursor = 1
    _with_state(s, lambda: _on_review_key(_FakeKeyEvent("d")))
    assert s.decisions["a.mp3"] is True
    assert s.decisions["b.mp3"] is False


def test_on_review_key_next_and_prev_move_cursor_and_set_keyboard_nav_used():
    s = IntegrationState()
    s.stage = 2
    s.entries = [_entry("a.mp3"), _entry("b.mp3"), _entry("c.mp3")]
    s.decisions = {"a.mp3": True, "b.mp3": True, "c.mp3": True}
    s.cursor = 0
    assert s.keyboard_nav_used is False
    _with_state(s, lambda: _on_review_key(_FakeKeyEvent("j")))
    assert s.cursor == 1
    assert s.keyboard_nav_used is True
    _with_state(s, lambda: _on_review_key(_FakeKeyEvent("ArrowDown")))
    assert s.cursor == 2
    _with_state(s, lambda: _on_review_key(_FakeKeyEvent("j")))
    assert s.cursor == 2  # clamped at the last entry
    _with_state(s, lambda: _on_review_key(_FakeKeyEvent("k")))
    assert s.cursor == 1


def test_on_review_key_ignores_keyup_and_repeat():
    s = IntegrationState()
    s.stage = 2
    s.entries = [_entry("a.mp3")]
    s.decisions = {"a.mp3": True}
    _with_state(s, lambda: _on_review_key(_FakeKeyEvent("d", keydown=False)))
    assert s.decisions["a.mp3"] is True
    _with_state(s, lambda: _on_review_key(_FakeKeyEvent("d", repeat=True)))
    assert s.decisions["a.mp3"] is True


def test_on_review_key_never_accepts_an_error_entry():
    """A/accept on the cursor must go through _decide's own error guard - a
    stray keypress on an unresolved-error card can't force an invalid accept."""
    s = IntegrationState()
    s.stage = 2
    s.entries = [_entry("err.mp3", status="error")]
    s.decisions = {}
    s.cursor = 0
    _with_state(s, lambda: _on_review_key(_FakeKeyEvent("a")))
    assert s.accepted == []


def _update_exec_status_on(state, line):
    """_update_exec_status reads/writes the module-level S singleton; swap it in
    for the duration of one assertion so tests don't share state."""
    import gui.tabs.integration as integration_module
    original = integration_module.S
    integration_module.S = state
    try:
        _update_exec_status(line)
    finally:
        integration_module.S = original

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
    IntegrationState, _esc, _failed_filename_from_output, _open_run_log,
    _sample_entries, _update_exec_status, _write_manifest, run_execute,
    run_execute_simulated, run_simulate,
)


def _entry(filename, **overrides):
    e = {
        "filename": filename, "artist": "Artist", "title": "Title", "album": "Album",
        "destination": "Artists/Artist", "reason": "", "isNewFolder": False,
        "status": "ok", "inBatchDuplicate": False, "tagChanges": [],
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


def test_sample_entries_cover_every_review_card_state():
    entries = _sample_entries()
    assert any(e["isNewFolder"] for e in entries)
    assert any(e["inBatchDuplicate"] for e in entries)
    assert any(e["tagChanges"] for e in entries)
    assert any(e["status"] == "error" for e in entries)
    assert any(not e["isNewFolder"] and not e["inBatchDuplicate"]
               and not e["tagChanges"] and e["status"] == "ok" for e in entries)


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


def test_run_execute_simulated_reproduces_partial_failure_labelling(monkeypatch):
    """One sample entry has status 'error' - the simulated run must halt there
    (like the real exe) and label the named file failed, the rest not run,
    exercising the exact same path real_execute's failure branch does."""
    import gui.tabs.integration as integration_module
    monkeypatch.setattr(integration_module, "show_error_modal", lambda *a, **k: None)

    state = IntegrationState()
    state.entries = _sample_entries()

    _with_state(state, lambda: asyncio.run(run_execute_simulated()))

    samples = _sample_entries()
    failed_entry = next(e for e in samples if e["status"] == "error")
    error_idx = samples.index(failed_entry)
    assert state.exec_status[failed_entry["filename"]] == "failed"
    assert any(v == "notrun" for v in state.exec_status.values())
    assert "not attempted" in state.exec_summary

    # Regression: entries processed BEFORE the failure got a real `[AUTO]`
    # success line and must show "done", never "notrun" - this is the bug
    # the 2026-09-03 Opus review found (_update_exec_status never matched
    # `[AUTO]` lines, so successful files were mislabelled not-run).
    for e in samples[:error_idx]:
        assert state.exec_status[e["filename"]] == "done"


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

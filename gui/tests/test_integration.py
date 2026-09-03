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
from gui.tabs.integration import IntegrationState, _esc, _open_run_log, _update_exec_status, _write_manifest, run_execute


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


def test_update_exec_status_marks_moved():
    s = IntegrationState()
    s.exec_targets = [_entry("Song.mp3")]
    s.exec_status = {"Song.mp3": "queued"}
    _update_exec_status_on(s, "[MOVED] Song.mp3 -> Artists/Artist/Song.mp3")
    assert s.exec_status["Song.mp3"] == "done"


def test_update_exec_status_marks_failed():
    s = IntegrationState()
    s.exec_targets = [_entry("Song.mp3")]
    s.exec_status = {"Song.mp3": "queued"}
    _update_exec_status_on(s, "[SKIPPED] Song.mp3 - duplicate")
    assert s.exec_status["Song.mp3"] == "failed"


def test_update_exec_status_overlapping_filenames_do_not_cross_contaminate():
    """A shorter accepted filename that is a substring of a longer one must NOT
    have its status flipped by a log line meant for the other file:
    _update_exec_status prefers the longest matching filename and ignores a
    substring match when a longer target also matches the same line."""
    s = IntegrationState()
    s.exec_targets = [_entry("Song.mp3"), _entry("Another Song.mp3")]
    s.exec_status = {"Song.mp3": "queued", "Another Song.mp3": "queued"}
    _update_exec_status_on(s, "[MOVED] Another Song.mp3 -> Artists/Artist/Another Song.mp3")
    assert s.exec_status["Another Song.mp3"] == "done"
    assert s.exec_status["Song.mp3"] == "queued"  # untouched: not the file this line is about


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

"""Fixture/demo mode: run the GUI against canned data with no library, no exe.

`python -m gui.main --fixture NAME` (or AM_GUI_FIXTURE=NAME) calls apply()
before state loads. Everything is redirected into a temp dir; the exe runner
is replaced by a stub that refuses to start, so a demo can never mutate the
library. Scenarios are listed in SCENARIOS.
"""
from __future__ import annotations

import json
import os
import shutil
import tempfile
from pathlib import Path

from gui import config

FIXTURES = Path(__file__).resolve().parent / "tests" / "fixtures"

SCENARIOS = {
    "normal": "Fixture stats and tracks; Acquire simulated; Integration idle",
    "empty": "No analysis files on disk (first run)",
    "error": "Corrupt analysis-stats.json (load error banner)",
    "long-names": "Very long titles, artists and albums",
    "huge": "Several thousand tracks (pagination)",
    "review": "Integration at the review stage with sample entries",
    "running": "Integration executing with a job in flight (busy)",
}

_active: dict = {"name": None, "dir": None}


def fixture_name() -> str | None:
    return _active["name"]


def is_demo() -> bool:
    return _active["name"] is not None


def _load(name: str) -> dict:
    return json.loads((FIXTURES / name).read_text(encoding="utf-8"))


def _write_tracks(path: Path, tracks: list[dict]) -> None:
    data = _load("tracks.json")
    data["tracks"] = tracks
    data["trackCount"] = len(tracks)
    path.write_text(json.dumps(data), encoding="utf-8")


def _redirect(tmp: Path) -> None:
    logs = tmp / "logs"
    cache = tmp / "cache"
    logs.mkdir()
    cache.mkdir()
    (tmp / "newmusic").mkdir()
    (tmp / "audiomirror").mkdir()
    config.LOGS_DIR = logs
    config.STATS_JSON = logs / "analysis-stats.json"
    config.TRACKS_JSON = logs / "tracks.json"
    config.CACHE_DIR = cache
    config.THUMBS_DIR = cache / "thumbs"
    config.STATS_HISTORY_JSON = cache / "stats-history.json"
    config.ACQUIRE_STATE_JSON = cache / "acquire-state.json"
    config.RUN_LOGS_DIR = cache / "run-logs"
    config.MANIFESTS_DIR = cache / "manifests"
    config.NEWMUSIC_DIR = tmp / "newmusic"
    config.AUDIOMIRROR_REPO = tmp / "audiomirror"
    rules = tmp / "tagfix-custom-rules.xml"
    src = config.TAGFIX_CUSTOM_RULES_XML
    if Path(src).exists():
        shutil.copyfile(src, rules)
    config.TAGFIX_CUSTOM_RULES_XML = rules


def _stub_runner() -> None:
    from gui import runner as runner_mod

    async def refuse(args, action="Operation", on_line=None, timeout=0):
        res = runner_mod.RunResult(command=["AudioManager.exe", *args])
        res.failed_to_start = "demo mode: the exe is disabled"
        return res

    runner_mod.runner.run = refuse


def apply(name: str) -> Path:
    """Redirect all state to a temp dir and seed the named scenario."""
    if name not in SCENARIOS:
        raise ValueError(f"unknown fixture {name!r}; choose from {', '.join(SCENARIOS)}")
    tmp = Path(tempfile.mkdtemp(prefix="am-demo-"))
    _active.update(name=name, dir=tmp)
    os.environ["AM_GUI_FIXTURE"] = name
    _redirect(tmp)
    _stub_runner()

    if name == "empty":
        return tmp
    if name == "error":
        config.STATS_JSON.write_text("{ this is not json", encoding="utf-8")
        return tmp

    shutil.copyfile(FIXTURES / "analysis-stats.json", config.STATS_JSON)
    base = _load("tracks.json")["tracks"]
    if name == "long-names":
        long = "Extraordinarily Long Name " * 8
        tracks = [dict(t, title=long + t.get("title", ""), artists=long, album=long) for t in base]
        _write_tracks(config.TRACKS_JSON, tracks)
    elif name == "huge":
        tracks = []
        for i in range(5000):
            t = dict(base[i % len(base)])
            t["id"] = f"\\Artists\\Gen{i}\\Album\\Track {i}.xml"
            t["title"] = f"Generated Track {i}"
            tracks.append(t)
        _write_tracks(config.TRACKS_JSON, tracks)
    else:
        shutil.copyfile(FIXTURES / "tracks.json", config.TRACKS_JSON)

    _seed_tabs(name)
    return tmp


def _seed_tabs(name: str) -> None:
    from gui.tabs import acquire, integration

    tracks = acquire._sample_tracks()
    keys = acquire._row_keys_for_tracks(tracks)
    acquire._state["tracks"] = tracks
    acquire._state["downloaded"] = {k: i % 2 == 0 for i, k in enumerate(keys)}
    acquire._state["extra"] = acquire._sample_extra()
    acquire._state["playlist_loaded"] = True
    acquire._state["simulated"] = True

    if name in ("review", "running"):
        S = integration.S
        S.entries = integration._sample_entries()
        S.simulated = True
        S.stage = 2
    if name == "running":
        from gui.runner import runner
        S = integration.S
        S.stage = 4
        S.exec_targets = list(S.accepted)
        S.exec_status = {e["filename"]: ("moving" if i == 0 else "queued")
                         for i, e in enumerate(S.exec_targets)}
        S.exec_done = False
        runner.busy = True
        runner.current_action = "Integrating (demo)"

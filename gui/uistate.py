"""State contract: JSON view of what each tab shows, with the row test ids the
DOM paints. Read-only; never touches the library or runs the exe.

Served at /state.json, /state/{tab}.json (see gui/api.py) and used by
gui/ui_check.py to compare painted data-testid values against these rows.
Contract doc: docs/References/GUI-State-Contract.md.
"""
from __future__ import annotations

import re
import time
import zlib

SCHEMA = 1

TAB_LABELS = [
    ("acquire", "Acquire"),
    ("integration", "Integration"),
    ("tagfix", "Tag Fix"),
    ("stats", "Statistics"),
    ("library", "Library"),
    ("mirror", "Mirror"),
    ("services", "Services"),
]

ROW_CAP = 500  # state rows per tab are capped; "row_count" always holds the true total


def testid(kind: str, key) -> str:
    """Stable DOM-safe id: kind + slug of key + 4 hex of crc32 of the raw key
    (keeps two keys that slug alike distinct)."""
    raw = str(key)
    slug = re.sub(r"[^A-Za-z0-9]+", "-", raw).strip("-").lower()[:48]
    return f"{kind}-{slug}-{zlib.crc32(raw.encode('utf-8')) & 0xFFFF:04x}"


def _now() -> str:
    return time.strftime("%Y-%m-%dT%H:%M:%S")


def _common(tab: str, label: str, rows: list[dict], **extra) -> dict:
    out = {
        "schema": SCHEMA, "tab": tab, "label": label, "generated_at": _now(),
        "stale": False, "poll_interval": None,
        "row_count": len(rows), "rows": rows[:ROW_CAP],
    }
    out.update(extra)
    return out


def _stats_state() -> dict:
    from gui.state import state
    rows = []
    s = state.stats
    stale = False
    if s is not None:
        for key in ("trackCount", "artistCount", "totalLibraryBytes", "genreCount",
                    "totalPlaybackHours", "avgSongLengthSeconds", "medianSongLengthSeconds",
                    "avgFileBytes"):
            rows.append({"testid": testid("stat", key), "key": key, "value": s.summary_num(key)})
    return _common("stats", "Statistics", rows, loaded=s is not None,
                   load_error=state.load_error, stats_generated_at=getattr(s, "generated_at", None),
                   stale=stale)


def _library_state() -> dict:
    from gui.state import state
    from gui.tabs.library import L
    rows = []
    total = 0
    if state.tracks is not None:
        page, total = state.tracks.query(search=L.search, genre=L.genre, decade=L.decade,
                                         page=L.page, page_size=L.page_size)
        for t in page:
            rows.append({"testid": testid("row-lib", t.get("id", "")), "id": t.get("id"),
                         "title": t.get("title"), "artists": t.get("artists")})
    return _common("library", "Library", rows, loaded=state.tracks is not None, total_matching=total,
                   view=L.view, page=L.page, page_size=L.page_size, search=L.search,
                   genre=L.genre, decade=L.decade)


def _integration_state() -> dict:
    from gui.tabs.integration import S
    rows = []
    for e in S.entries:
        fn = e.get("filename", "")
        rows.append({"testid": testid("row-int", fn), "filename": fn, "title": e.get("title"),
                     "artist": e.get("artist"), "destination": e.get("destination"),
                     "status": e.get("status"), "accepted": bool(S.decisions.get(fn, True)),
                     "new_folder": bool(e.get("isNewFolder"))})
    return _common("integration", "Integration", rows, stage=S.stage, filter=S.filter,
                   simulated=S.simulated, triage=S.triage, accepted_count=len(S.accepted),
                   declined_count=len(S.declined), exec_done=S.exec_done, exec_ok=S.exec_ok,
                   exec_summary=S.exec_summary, exec_record_missing=S.exec_record_missing)


def _acquire_state() -> dict:
    from gui.tabs.acquire import _row_keys_for_tracks, _state
    tracks = _state["tracks"]
    keys = _row_keys_for_tracks(tracks)
    rows = []
    for key, (artist, title, *_rest) in zip(keys, tracks):
        rows.append({"testid": testid("row-acq", key), "key": key, "artist": artist, "title": title,
                     "downloaded": bool(_state["downloaded"].get(key, False)),
                     "overridden": key in _state["manual_override"]})
    extra = [{"testid": testid("row-acq-extra", str(p)), "artist": a, "title": t}
             for a, t, _u, p in _state["extra"]]
    return _common("acquire", "Acquire", rows + extra, track_count=len(tracks), extra_count=len(extra),
                   playlist_loaded=_state["playlist_loaded"], simulated=_state["simulated"],
                   hide_downloaded=_state["hide_downloaded"], fetching=_state["fetch_progress"],
                   sync_busy=_state["sync_busy"])


def _tagfix_state() -> dict:
    from gui.tabs.tagfix import FIXED_RULES, T
    rows = [{"testid": testid("row-rule", r[0]), "name": r[0], "status": r[2]} for r in FIXED_RULES]
    return _common("tagfix", "Tag Fix", rows, dry_run_lines=len(T.lines))


def _mirror_state() -> dict:
    from gui import config
    return _common("mirror", "Mirror", [], repo=str(config.AUDIOMIRROR_REPO))


def _services_state() -> dict:
    return _common("services", "Services", [])


_BUILDERS = {
    "stats": _stats_state, "library": _library_state, "integration": _integration_state,
    "acquire": _acquire_state, "tagfix": _tagfix_state, "mirror": _mirror_state,
    "services": _services_state,
}


def tab_state(tab: str) -> dict | None:
    fn = _BUILDERS.get(tab)
    if fn is None:
        return None
    try:
        return fn()
    except Exception as e:  # a broken tab must not break the whole contract
        out = _common(tab, dict(TAB_LABELS)[tab], [])
        out["error"] = f"{type(e).__name__}: {e}"
        return out


def full_state() -> dict:
    from gui import buildinfo, demo
    from gui.runner import runner
    return {
        "schema": SCHEMA, "generated_at": _now(),
        "build": buildinfo.api_payload(demo.fixture_name()),
        "fixture": demo.fixture_name(),
        "busy": {"busy": runner.busy, "action": runner.current_action},
        "tabs": {k: tab_state(k) for k, _ in TAB_LABELS},
    }

"""Parser for the exe's dry-run routing JSON (logs/routing-<timestamp>.json).

Contract (MusicIntegrator.BuildJson):
{"summary": {routes{}, miscAutoMigrations[], miscAutoMigrationTotal,
 compilationAlbums[]}, "files": [ {filename, artist, title, album, destination,
 reason, isNewFolder, status, inBatchDuplicate, compilationAlbum,
 libraryDuplicate, dupLibraryPath, dupLibraryTrack, dupLibraryAlbum,
 dupNewAlbum, dupRecommendationKey, dupRecommendation, dupReason,
 tagChanges[]} ]}. Defensive: malformed entries are dropped, missing fields
default to safe values.

A bare top-level array is the pre-summary shape and is still read as the file
list, so a routing JSON written by an older exe build still opens; it simply
carries no batch summary.

The summary is batch-level scan-ahead context that no single row can express:
which destination categories the batch spreads across, which artists cross the
3-song threshold and therefore have existing Misc songs auto-migrated (files
ALREADY in the library that the run will move, not just incoming ones), and
which albums were detected as batch compilations (3+ distinct primary artists
on one album). See docs/Development/IDEAS.md "Scan-ahead batch context is
invisible".

inBatchDuplicate and libraryDuplicate are two distinct concepts (see
docs/Development/IDEAS.md "Duplicate-resolution UI"): inBatchDuplicate means
"same artist+title appears twice within this NewMusic batch" (unchanged,
MarkInBatchDuplicates); libraryDuplicate means "this file already exists
somewhere in the library" and carries the dup* fields describing the
exe's D/L/K recommendation for review-stage resolution.
"""
from __future__ import annotations

import json
import re
from pathlib import Path

from gui import config


def empty_summary() -> dict:
    """A fresh empty summary. Built per call, never a shared constant handed
    out by reference - the nested containers are mutable and a caller that
    edits one must not be able to poison the next parse."""
    return {
        "routes": {},
        "miscAutoMigrations": [],
        "miscAutoMigrationTotal": 0,
        "compilationAlbums": [],
    }


#: Read-only reference shape for tests and callers comparing against "no batch
#: context". Never returned directly - see empty_summary().
EMPTY_SUMMARY = empty_summary()


def _file_rows(raw) -> list:
    """The per-file rows, from either contract shape. A bare list is the
    pre-summary shape; the current shape nests them under "files"."""
    if isinstance(raw, list):
        return raw
    if isinstance(raw, dict):
        files = raw.get("files")
        return files if isinstance(files, list) else []
    return []


def parse_routing_document(path: Path) -> tuple[list[dict], dict]:
    """Both halves of the routing JSON in one read: (file entries, batch summary)."""
    with open(path, encoding="utf-8-sig") as f:
        raw = json.load(f)
    return _parse_entries(_file_rows(raw)), _parse_summary(raw)


def parse_batch_summary(path: Path) -> dict:
    """Batch-level scan-ahead context. Always the full key set - callers never
    branch on key presence, only on emptiness."""
    with open(path, encoding="utf-8-sig") as f:
        return _parse_summary(json.load(f))


def _parse_summary(raw) -> dict:
    s = raw.get("summary") if isinstance(raw, dict) else None
    if not isinstance(s, dict):
        return empty_summary()

    raw_routes = s.get("routes")
    routes = {}
    if isinstance(raw_routes, dict):
        for name, count in raw_routes.items():
            if isinstance(name, str) and name and isinstance(count, int) \
                    and not isinstance(count, bool) and count > 0:
                routes[name] = count

    migrations = []
    for m in s.get("miscAutoMigrations") or []:
        if not isinstance(m, dict):
            continue
        artist, count = m.get("artist"), m.get("count")
        if isinstance(artist, str) and artist and isinstance(count, int) \
                and not isinstance(count, bool) and count > 0:
            migrations.append({"artist": artist, "count": count})

    total = s.get("miscAutoMigrationTotal")
    if not isinstance(total, int) or isinstance(total, bool) or total < 0:
        # Never trust a malformed total over the rows it is supposed to summarise.
        total = sum(m["count"] for m in migrations)

    comps = [a for a in s.get("compilationAlbums") or [] if isinstance(a, str) and a]
    return {
        "routes": routes,
        "miscAutoMigrations": migrations,
        "miscAutoMigrationTotal": total,
        "compilationAlbums": comps,
    }


def parse_routing_file(path: Path) -> list[dict]:
    with open(path, encoding="utf-8-sig") as f:
        raw = json.load(f)
    return _parse_entries(_file_rows(raw))


def _parse_entries(rows: list) -> list[dict]:
    entries = []
    for e in rows:
        if not isinstance(e, dict) or not isinstance(e.get("filename"), str):
            continue
        entries.append({
            "filename": e["filename"],
            "artist": e.get("artist") or "",
            "title": e.get("title") or "",
            "album": e.get("album") or "",
            "destination": e.get("destination") or "",
            "reason": e.get("reason") or "",
            "isNewFolder": bool(e.get("isNewFolder")),
            "status": e.get("status") or "",
            "inBatchDuplicate": bool(e.get("inBatchDuplicate")),
            "compilationAlbum": bool(e.get("compilationAlbum")),
            "libraryDuplicate": bool(e.get("libraryDuplicate")),
            "dupLibraryPath": e.get("dupLibraryPath") or "",
            "dupLibraryTrack": e.get("dupLibraryTrack") or "",
            "dupLibraryAlbum": e.get("dupLibraryAlbum") or "",
            "dupNewAlbum": e.get("dupNewAlbum") or "",
            "dupRecommendationKey": e.get("dupRecommendationKey") or "",
            "dupRecommendation": e.get("dupRecommendation") or "",
            "dupReason": e.get("dupReason") or "",
            "tagChanges": [t for t in e.get("tagChanges") or [] if isinstance(t, str)],
        })
    return entries


def routing_path_from_output(lines: list[str]) -> Path | None:
    """The exe prints '  JSON: <path>' after writing the file - the exact
    artifact of THIS run. Falls back to the newest routing-*.json in logs/."""
    for ln in reversed(lines):
        m = re.search(r"JSON:\s*(.+routing-[\d-]+\.json)", ln)
        if m:
            p = Path(m.group(1).strip())
            if p.exists():
                return p
    candidates = sorted(config.LOGS_DIR.glob("routing-*.json"),
                        key=lambda p: p.stat().st_mtime, reverse=True)
    return candidates[0] if candidates else None


def parse_projected_libchecker(lines: list[str]) -> dict | None:
    """The dry run's actual safety verdict (`MusicIntegrator.cs` ~1941-2033):
    a "Projected LibChecker (Dry Run)" header, a projected-count summary line,
    then either " - LibChecker: Clean" or a run of issue lines each ending in
    zero or more " - Total hits: N" subtotals. Returns None if the section
    never printed (e.g. RunProjectedLibChecker's own "could not load current
    library tags" SKIP path)."""
    start = next((i for i, ln in enumerate(lines) if "Projected LibChecker (Dry Run)" in ln), None)
    if start is None:
        return None
    summary = ""
    clean = False
    skipped = False
    total_hits = 0
    for ln in lines[start:]:
        if " - SKIP:" in ln:
            skipped = True
            summary = ln.strip().lstrip("-").strip()
            break
        if " - Projected library:" in ln:
            summary = ln.strip().lstrip("-").strip()
        elif "LibChecker: Clean" in ln:
            clean = True
        elif " - Time taken:" in ln:
            break
        else:
            m = re.search(r"Total hits:\s*(\d+)", ln)
            if m:
                total_hits += int(m.group(1))
    return {"summary": summary, "clean": clean, "skipped": skipped, "total_hits": total_hits}


def parse_confidence_report(lines: list[str]) -> dict | None:
    """The exe's strongest post-run guarantee that a claimed-successful real
    run actually succeeded (`PrintConfidenceReport` in `MusicIntegrator.cs`
    ~900-990): a "Files in NewMusic: N | Moved: M | Skipped: S" count line
    (flagged with "[ERROR] Count mismatch!" if the counts don't reconcile),
    then a destination sanity check that re-reads every moved file with
    TagLib and reports "[ERROR] Destination sanity check FAILED" plus a
    [MISSING]/[UNREADABLE] line per bad file, or a single "all N moved
    file(s) exist and are readable" line when clean. Returns None if the
    section never printed (dry runs never reach the sanity-check step)."""
    start = next((i for i, ln in enumerate(lines) if "CONFIDENCE REPORT" in ln), None)
    if start is None:
        return None
    count_line = ""
    count_ok = True
    sanity_ok = True
    sanity_summary = ""
    error_count = 0
    for ln in lines[start:]:
        s = ln.strip()
        if s.startswith("Files in NewMusic:"):
            count_line = s
        elif "[ERROR] Count mismatch!" in ln:
            count_ok = False
        elif "[ERROR] Destination sanity check FAILED" in ln:
            sanity_ok = False
        elif s.startswith("Sanity check:"):
            sanity_summary = s
        elif s.startswith("[ERRORS:"):
            m = re.search(r"\[ERRORS:\s*(\d+)\]", s)
            if m:
                error_count = int(m.group(1))
    return {
        "count_line": count_line, "count_ok": count_ok,
        "sanity_ok": sanity_ok, "sanity_summary": sanity_summary,
        "error_count": error_count,
    }


def newmusic_path(filename: str) -> Path:
    """Absolute path of a scanned file in the NewMusic inbox (read-only use:
    album-art extraction). filename may already be relative with subfolders."""
    return config.NEWMUSIC_DIR / filename

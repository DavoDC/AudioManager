"""Parser for the exe's dry-run routing JSON (logs/routing-<timestamp>.json).

Contract (MusicIntegrator.BuildJson):
{"summary": {routes{}, miscAutoMigrations[], miscAutoMigrationTotal,
 compilationAlbums[]}, "files": [ {filename, artist, title, album, destination,
 reason, isNewFolder, status, inBatchDuplicate, compilationAlbum,
 libraryDuplicate, dupLibraryPath, dupLibraryTrack, dupLibraryAlbum,
 dupNewAlbum, dupRecommendationKey, dupRecommendation, dupReason,
 tagChanges[]} ]}. Defensive: malformed entries are dropped, missing fields
default to safe values.

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
from gui.data_loader import SchemaVersionError

#: schemaVersion this GUI was built for (routing/execution records share one
#: contract - see docs/References/Execution-Record-Contract-Design.md).
ROUTING_SCHEMA_VERSION = 1


def _check_version(raw, path: Path) -> None:
    """Same discipline as data_loader._check_schema: a missing or mismatched
    schemaVersion is a hard failure, never a silent mis-read. raw must be a
    dict - the pre-versioned bare-array shape is no longer accepted (see
    routing_path_from_output's "since" note and section 4.2 of the contract
    design doc)."""
    found = raw.get("schemaVersion") if isinstance(raw, dict) else None
    if found != ROUTING_SCHEMA_VERSION:
        raise SchemaVersionError(path.name, found, ROUTING_SCHEMA_VERSION)


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
    """The per-file rows. The contract nests them under "files" - the older
    bare-top-level-array shape is no longer accepted (schemaVersion is now
    mandatory, so an unversioned file already fails _check_version first)."""
    if isinstance(raw, dict):
        files = raw.get("files")
        return files if isinstance(files, list) else []
    return []


def parse_routing_document(path: Path) -> tuple[list[dict], dict]:
    """Both halves of the routing JSON in one read: (file entries, batch summary)."""
    with open(path, encoding="utf-8-sig") as f:
        raw = json.load(f)
    _check_version(raw, path)
    return _parse_entries(_file_rows(raw)), _parse_summary(raw)


def parse_batch_summary(path: Path) -> dict:
    """Batch-level scan-ahead context. Always the full key set - callers never
    branch on key presence, only on emptiness."""
    with open(path, encoding="utf-8-sig") as f:
        raw = json.load(f)
    _check_version(raw, path)
    return _parse_summary(raw)


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
    _check_version(raw, path)
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
            "detail": e.get("detail") or "",
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


def routing_path_from_output(lines: list[str], since: float) -> Path | None:
    """The exe prints '  JSON: <path>' after writing the file - the exact
    artifact of THIS run. Falls back to the newest routing-*.json in logs/,
    but only among files written at or after `since` (a time.time() captured
    by the caller before the exe was launched) - a stale routing JSON from an
    earlier run must never be misread as this run's output."""
    for ln in reversed(lines):
        m = re.search(r"JSON:\s*(.+routing-[\d-]+\.json)", ln)
        if m:
            p = Path(m.group(1).strip())
            if p.exists():
                return p
    candidates = sorted(
        (p for p in config.LOGS_DIR.glob("routing-*.json") if p.stat().st_mtime >= since),
        key=lambda p: p.stat().st_mtime, reverse=True)
    return candidates[0] if candidates else None


def execution_record_path(lines: list[str], since: float) -> Path | None:
    """Mirrors routing_path_from_output for the real-run execution record:
    the exe prints '  EXECUTION JSON: <path>' after writing it - the exact
    artifact of THIS run. Falls back to the newest execution-*.json in
    logs/ written at or after `since`, else None."""
    for ln in reversed(lines):
        m = re.search(r"EXECUTION JSON:\s*(.+execution-[\d-]+\.json)", ln)
        if m:
            p = Path(m.group(1).strip())
            if p.exists():
                return p
    candidates = sorted(
        (p for p in config.LOGS_DIR.glob("execution-*.json") if p.stat().st_mtime >= since),
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


def parse_execution_record(path: Path) -> dict:
    """The real run's structured post-run outcome (docs/References/
    Execution-Record-Contract-Design.md), replacing console-prose parsing:
    schemaVersion/recordType/dryRun checked so a stale or dry-run document
    can never be misread as a real outcome, then the same summary/entries
    parsing routing documents already use plus the new confidence block."""
    with open(path, encoding="utf-8-sig") as f:
        raw = json.load(f)
    _check_version(raw, path)
    record_type = raw.get("recordType")
    dry_run = raw.get("dryRun")
    if record_type != "execution" or dry_run is not False:
        # A dry-run document must never be interpretable as a real outcome -
        # reuse SchemaVersionError so this failure mode fails exactly as
        # loudly as a genuine version mismatch, with found = the wrong value.
        found = record_type if record_type != "execution" else dry_run
        raise SchemaVersionError(path.name, found, ROUTING_SCHEMA_VERSION)
    return {
        "generatedAt": raw.get("generatedAt") or "",
        "summary": _parse_summary(raw),
        "confidence": _parse_confidence(raw.get("confidence")),
        "files": _parse_entries(_file_rows(raw)),
    }


def _parse_confidence(raw) -> dict | None:
    """Defensive parse of the "confidence" block (null on a dry run, absent
    on any file predating this contract) - always the full key set when
    present, same discipline as _parse_summary, so callers never have to
    branch on key presence."""
    if not isinstance(raw, dict):
        return None

    count_check = raw.get("countCheck") if isinstance(raw.get("countCheck"), dict) else {}
    sanity_check = raw.get("sanityCheck") if isinstance(raw.get("sanityCheck"), dict) else {}

    def _int(d: dict, key: str) -> int | None:
        v = d.get(key)
        return v if isinstance(v, int) and not isinstance(v, bool) else None

    failures = []
    for f in sanity_check.get("failures") or []:
        if not isinstance(f, dict):
            continue
        dest, reason = f.get("destination"), f.get("reason")
        if isinstance(dest, str) and dest and reason in ("missing", "unreadable"):
            failures.append({"destination": dest, "reason": reason})

    new_folders = [f for f in raw.get("newFolders") or [] if isinstance(f, str) and f]

    errors = []
    for e in raw.get("errors") or []:
        if not isinstance(e, dict):
            continue
        filename, detail = e.get("filename"), e.get("detail")
        if isinstance(filename, str) and filename:
            errors.append({"filename": filename, "detail": detail if isinstance(detail, str) else ""})

    error_count = _int(raw, "errorCount")
    if error_count is None or error_count < 0:
        # Never trust a malformed count over the rows it is supposed to summarise.
        error_count = len(errors)

    return {
        "count_ok": bool(count_check.get("ok")),
        "sanity_ran": bool(sanity_check.get("ran")),
        "sanity_ok": bool(sanity_check.get("ok")),
        "sanity_checked": _int(sanity_check, "checked") or 0,
        "sanity_failures": failures,
        "new_folders": new_folders,
        "error_count": error_count,
        "errors": errors,
        "total_count": _int(count_check, "totalFiles"),
        "moved_count": _int(count_check, "moved"),
        "skipped_count": _int(count_check, "skipped"),
    }


def newmusic_path(filename: str) -> Path:
    """Absolute path of a scanned file in the NewMusic inbox (read-only use:
    album-art extraction). filename may already be relative with subfolders."""
    return config.NEWMUSIC_DIR / filename

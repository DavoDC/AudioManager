"""Parser for the exe's dry-run routing JSON (logs/routing-<timestamp>.json).

Contract (MusicIntegrator.WriteJsonOutput): array of
{filename, artist, title, album, destination, reason, isNewFolder, status,
 inBatchDuplicate, tagChanges[]}. Defensive: malformed entries are dropped,
missing fields default to safe values.
"""
from __future__ import annotations

import json
import re
from pathlib import Path

from gui import config


def parse_routing_file(path: Path) -> list[dict]:
    with open(path, encoding="utf-8-sig") as f:
        raw = json.load(f)
    if not isinstance(raw, list):
        return []
    entries = []
    for e in raw:
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


def newmusic_path(filename: str) -> Path:
    """Absolute path of a scanned file in the NewMusic inbox (read-only use:
    album-art extraction). filename may already be relative with subfolders."""
    return config.NEWMUSIC_DIR / filename

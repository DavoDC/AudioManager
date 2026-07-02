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


def newmusic_path(filename: str) -> Path:
    """Absolute path of a scanned file in the NewMusic inbox (read-only use:
    album-art extraction). filename may already be relative with subfolders."""
    return config.NEWMUSIC_DIR / filename

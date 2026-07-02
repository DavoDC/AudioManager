"""Integration-batch boundaries derived from AudioMirror git history.

The brief's canonical rule: everywhere a "batch" appears (Recent Additions,
delta tiles, the per-batch bar chart) the boundaries come from AudioMirror
commit history - commits that ADD track XML files. Read-only git commands
only; this module never writes to AudioMirror.

A batch = all track XMLs added on one calendar date (multiple commits on the
same day collapse into one batch, matching how an integration run commits).
"""
from __future__ import annotations

import subprocess
from pathlib import Path

from gui import config


def _mirror_path_to_track_id(path: str) -> str:
    """Convert a git path (AUDIO_MIRROR/Artists/X/a.xml) to the tracks.json
    id format (\\Artists\\X\\a.xml - mirror-relative, backslashes)."""
    p = path.replace("/", "\\")
    prefix = "AUDIO_MIRROR\\"
    if p.startswith(prefix):
        p = p[len(prefix) - 1 :]  # keep the leading backslash
    elif not p.startswith("\\"):
        p = "\\" + p
    return p


def get_batches(repo: Path | None = None, limit: int = 400) -> list[dict]:
    """Batches sorted newest-first: [{date, count, ids}].

    Returns [] on any git failure (missing repo, git not on PATH) - the UI
    shows an empty state rather than crashing.
    """
    repo = repo or config.AUDIOMIRROR_REPO
    try:
        out = subprocess.run(
            [
                "git", "-C", str(repo), "log",
                f"-{limit}",
                "--diff-filter=A",
                "--name-only",
                "--pretty=format:@@%ad",
                "--date=format:%Y-%m-%d",
                "--", "AUDIO_MIRROR",
            ],
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=30,
        )
    except (OSError, subprocess.TimeoutExpired):
        return []
    if out.returncode != 0:
        return []

    by_date: dict[str, list[str]] = {}
    order: list[str] = []
    current_date: str | None = None
    for line in out.stdout.splitlines():
        line = line.strip()
        if line.startswith("@@"):
            current_date = line[2:]
            continue
        if not line or current_date is None or not line.lower().endswith(".xml"):
            continue
        if current_date not in by_date:
            by_date[current_date] = []
            order.append(current_date)
        by_date[current_date].append(_mirror_path_to_track_id(line))

    return [
        {"date": d, "count": len(by_date[d]), "ids": by_date[d]}
        for d in order
    ]

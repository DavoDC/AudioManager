"""Which build is running: repo name, short sha, commit date, dirty marker, boot id.

Standard library only. One source for the footer, /api/build, the CLI start-up line and
the capture harness, so they cannot disagree. Git runs once (cached); it never raises and
never runs inside a request handler (WS-034). Outside a git checkout the sha is "unknown".
"""
from __future__ import annotations

import subprocess
import uuid
from datetime import datetime
from pathlib import Path

REPO_NAME = "AudioManager"
REPO_ROOT = Path(__file__).resolve().parents[1]

_NO_WINDOW = getattr(subprocess, "CREATE_NO_WINDOW", 0)
# New value every process start; a hot reload re-execs, so an open page sees it change.
BOOT_ID = uuid.uuid4().hex[:12]
STARTED_AT = datetime.now().astimezone().isoformat(timespec="seconds")
_cache: dict[str, dict] = {}


def _git(root: Path, *args: str) -> str | None:
    try:
        out = subprocess.run(["git", "-C", str(root), *args], capture_output=True, text=True,
                             timeout=5, creationflags=_NO_WINDOW)
    except (OSError, subprocess.SubprocessError):
        return None
    return out.stdout if out.returncode == 0 else None


def _describe(root: Path) -> dict:
    sha = date = "unknown"
    dirty = False
    head = _git(root, "log", "-1", "--format=%h %cs")
    if head and len(head.split()) == 2:
        sha, date = head.split()
        # Tracked-file changes only; untracked logs and caches must not read as "dirty code".
        status = _git(root, "status", "--porcelain", "--untracked-files=no", "--", "gui", "project", "scripts")
        dirty = bool(status and status.strip())
    return {"repo": REPO_NAME, "sha": sha, "date": date, "dirty": dirty}


def describe(repo_root: Path | None = None, refresh: bool = False) -> dict:
    root = Path(repo_root or REPO_ROOT)
    key = str(root)
    if refresh or key not in _cache:
        _cache[key] = _describe(root)
    return dict(_cache[key])


def format_line(info: dict, fixture: str | None = None) -> str:
    if info["sha"] == "unknown":
        line = f'{info["repo"]} unknown'
    else:
        line = f'{info["repo"]} {info["sha"]}{"*" if info.get("dirty") else ""} {info["date"]}'
    return line + (f" FIXTURE {fixture}" if fixture else "")


def api_payload(fixture: str | None = None) -> dict:
    info = describe()
    return {"line": format_line(info, fixture), "sha": info["sha"], "dirty": info["dirty"],
            "date": info["date"], "boot_id": BOOT_ID, "started_at": STARTED_AT,
            "fixture": fixture}



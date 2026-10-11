"""Claude-visible activity log: one plain-text file per day, append only.

Lives in gui/.cache/log/YYYY-MM-DD.log (gitignored). Holds browser
[app-error] lines (POST /__log), server-side tab build failures, start-up and
shutdown lines. Read it with `python -m gui.cli log` or just open the file.
"""
from __future__ import annotations

import threading
import time
from pathlib import Path

from gui import config

_lock = threading.Lock()


def log_dir() -> Path:
    return config.CACHE_DIR / "log"


def path_today() -> Path:
    return log_dir() / (time.strftime("%Y-%m-%d") + ".log")


def write(kind: str, message: str) -> None:
    """Never raises: logging must not break the app."""
    try:
        line = f"{time.strftime('%H:%M:%S')} [{kind}] {' '.join(str(message).split())[:2000]}\n"
        with _lock:
            log_dir().mkdir(parents=True, exist_ok=True)
            with open(path_today(), "a", encoding="utf-8") as f:
                f.write(line)
    except Exception:
        pass


def tail(n: int = 50) -> list[str]:
    try:
        return path_today().read_text(encoding="utf-8").splitlines()[-n:]
    except OSError:
        return []

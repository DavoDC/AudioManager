"""State history (WS-064): the last N compact snapshots of the state contract.

A snapshot is {seq, at, verdict, busy, action, tabs: {tab: {row_count, digest}}}: enough
to see WHAT changed and WHEN without storing every row. A new snapshot is recorded only
when something differs from the previous one. /state/history.json?since=<seq> returns
just the newer ones, so Claude can poll for changes instead of diffing whole states.
In-memory by design: a restart starts a fresh history (boot_id in /api/build tells you).
"""
from __future__ import annotations

import json
import threading
import time
import zlib
from collections import deque

DEFAULT_SIZE = 50
SAMPLE_SECONDS = 15.0


def _digest(rows) -> str:
    return format(zlib.crc32(json.dumps(rows, sort_keys=True, default=str).encode("utf-8")), "08x")


class History:
    def __init__(self, size: int = DEFAULT_SIZE):
        self.size = size
        self._snaps: deque[dict] = deque(maxlen=size)
        self._seq = 0
        self._lock = threading.Lock()

    @staticmethod
    def _summarise(state: dict, verdict: str) -> dict:
        busy = state.get("busy") or {}
        return {"verdict": verdict, "busy": bool(busy.get("busy")), "action": busy.get("action"),
                "tabs": {tab: {"row_count": s.get("row_count", 0), "digest": _digest(s.get("rows", []))}
                         for tab, s in (state.get("tabs") or {}).items() if s}}

    def record(self, state: dict, verdict: str) -> dict | None:
        body = self._summarise(state, verdict)
        with self._lock:
            if self._snaps and all(self._snaps[-1][k] == body[k] for k in body):
                return None
            self._seq += 1
            snap = {"seq": self._seq, "at": time.strftime("%Y-%m-%dT%H:%M:%S"), **body}
            self._snaps.append(snap)
            return snap

    def since(self, cursor: int) -> list[dict]:
        with self._lock:
            return [dict(s) for s in self._snaps if s["seq"] > cursor]

    def payload(self, cursor: int = 0) -> dict:
        with self._lock:
            latest = self._seq
        return {"cursor": latest, "size": self.size, "snapshots": self.since(cursor)}


history = History()


def sample() -> dict | None:
    """Take one snapshot of the live state (never raises)."""
    try:
        from gui import health, uistate
        return history.record(uistate.full_state(), health.compute()["verdict"])
    except Exception:
        return None


def start_sampler(interval: float = SAMPLE_SECONDS) -> threading.Thread:
    def loop() -> None:
        while True:
            sample()
            time.sleep(interval)

    t = threading.Thread(target=loop, name="am-history", daemon=True)
    t.start()
    return t

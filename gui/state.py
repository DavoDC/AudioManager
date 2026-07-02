"""Shared application state: loaded contract data + refresh orchestration.

One AppState per server process (single-user local tool). Tabs read from it
and subscribe to reload notifications after an analysis run completes.
"""
from __future__ import annotations

from typing import Callable

from gui import batches as batches_mod
from gui import config, data_loader
from gui.data_loader import SchemaVersionError, Stats, StatsHistory, TrackIndex


class AppState:
    def __init__(self):
        self.stats: Stats | None = None
        self.tracks: TrackIndex | None = None
        self.batches: list[dict] = []
        self.history = StatsHistory()
        self.load_error: str | None = None
        self._listeners: list[Callable[[], None]] = []

    # ------------------------------------------------------------- loading

    def load(self) -> None:
        """(Re)load both JSON contracts + batch boundaries from disk.
        Fast (file reads only) - the exe is only run via explicit controls."""
        self.load_error = None
        try:
            self.stats = data_loader.load_stats()
        except FileNotFoundError:
            self.stats = None
            self.load_error = (
                "analysis-stats.json not found - run analysis once "
                "(Statistics header > Re-run analysis) to generate it."
            )
        except (SchemaVersionError, ValueError) as e:
            self.stats = None
            self.load_error = str(e)

        try:
            self.tracks = data_loader.load_tracks()
        except FileNotFoundError:
            self.tracks = None
            if not self.load_error:
                self.load_error = "tracks.json not found - run analysis once to generate it."
        except (SchemaVersionError, ValueError) as e:
            self.tracks = None
            self.load_error = self.load_error or str(e)

        self.batches = batches_mod.get_batches()
        self._record_history()

    def _record_history(self) -> None:
        """Snapshot the current summary keyed by the latest git batch, so
        future runs can diff 'vs last batch'."""
        if self.stats and self.batches:
            self.history.record(self.batches[0]["date"], self.stats.summary)

    def notify_reload(self) -> None:
        for cb in list(self._listeners):
            try:
                cb()
            except Exception:
                pass

    def on_reload(self, cb: Callable[[], None]) -> None:
        self._listeners.append(cb)

    # -------------------------------------------------------------- deltas

    def batch_delta(self, key: str) -> float | None:
        """current summary[key] - previous batch's stored summary[key]."""
        if not (self.stats and self.batches):
            return None
        prev = self.history.previous_summary(self.batches[0]["date"])
        if not prev or key not in prev:
            return None
        cur = self.stats.summary.get(key)
        if not isinstance(cur, (int, float)) or not isinstance(prev[key], (int, float)):
            return None
        return cur - prev[key]

    # --------------------------------------------------------- batch views

    def batch_tracks(self, batch: dict) -> list[dict]:
        """Resolve a batch's added-XML ids to tracks.json rows (best-effort:
        renamed/removed tracks simply don't resolve)."""
        if not self.tracks:
            return []
        out = []
        for tid in batch.get("ids", []):
            t = self.tracks.by_id(tid)
            if t:
                out.append(t)
        return out


state = AppState()

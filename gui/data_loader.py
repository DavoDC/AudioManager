"""Single data-access module for the AudioManager GUI.

Owns the two C#-emitted JSON contracts:
  - logs/analysis-stats.json  (aggregate statistics -> Statistics tab)
  - logs/tracks.json          (per-track array -> Library Browser)
plus the GUI-owned batch-delta cache (gui/.cache/stats-history.json).

HARD RULE: this module (and the whole GUI) never parses AudioMirror XML.
C# is the only XML parser; these JSON files are the only data sources.

Every accessor is defensive: missing or malformed fields return safe
defaults ([] / 0 / None) instead of raising - except schemaVersion, which
must fail loudly on a mismatch (silent mis-reads are worse than a crash).
"""
from __future__ import annotations

import json
from pathlib import Path
from typing import Any, Callable

from gui import config


class SchemaVersionError(Exception):
    """The JSON file's schemaVersion is not the one this GUI was built for."""

    def __init__(self, file: str, found: Any, expected: int):
        self.file = file
        self.found = found
        self.expected = expected
        super().__init__(
            f"{file}: schemaVersion {found!r} does not match the version this GUI "
            f"was built for ({expected}). Refusing to mis-read - the GUI needs an "
            f"update for the new schema."
        )


def _read_json(path: Path) -> Any:
    with open(path, encoding="utf-8-sig") as f:
        return json.load(f)


def _check_schema(data: dict, path: Path, expected: int) -> None:
    found = data.get("schemaVersion") if isinstance(data, dict) else None
    if found != expected:
        raise SchemaVersionError(path.name, found, expected)


# ---------------------------------------------------------------- statistics


class Stats:
    """Typed, defensive accessors over analysis-stats.json."""

    def __init__(self, data: dict):
        self._d = data if isinstance(data, dict) else {}

    @property
    def generated_at(self) -> str | None:
        v = self._d.get("generatedAt")
        return v if isinstance(v, str) else None

    @property
    def summary(self) -> dict:
        s = self._d.get("summary")
        return s if isinstance(s, dict) else {}

    def summary_num(self, key: str, default: float = 0) -> float:
        v = self.summary.get(key)
        return v if isinstance(v, (int, float)) and not isinstance(v, bool) else default

    def _dist(self, key: str) -> list[dict]:
        """A distribution: list of {label, count}, invalid entries dropped."""
        raw = self._d.get(key)
        if not isinstance(raw, list):
            return []
        out = []
        for item in raw:
            if (
                isinstance(item, dict)
                and isinstance(item.get("label"), str)
                and isinstance(item.get("count"), (int, float))
            ):
                out.append({"label": item["label"], "count": item["count"]})
        return out

    @property
    def genre_distribution(self) -> list[dict]:
        return self._dist("genreDistribution")

    @property
    def decade_distribution(self) -> list[dict]:
        return self._dist("decadeDistribution")

    @property
    def year_distribution(self) -> list[dict]:
        return self._dist("yearDistribution")

    @property
    def age_distribution(self) -> list[dict]:
        return self._dist("ageDistribution")

    def top_artists(self, mode: str) -> list[dict]:
        """mode: 'exclMusivation' or 'all'."""
        block = self._d.get("topArtists")
        if not isinstance(block, dict):
            return []
        raw = block.get(mode)
        if not isinstance(raw, list):
            return []
        return [
            {"label": i["label"], "count": i["count"]}
            for i in raw
            if isinstance(i, dict)
            and isinstance(i.get("label"), str)
            and isinstance(i.get("count"), (int, float))
        ]

    @property
    def age_stats(self) -> dict:
        v = self._d.get("ageStats")
        return v if isinstance(v, dict) else {}

    @property
    def cover_art(self) -> dict:
        v = self._d.get("coverArt")
        return v if isinstance(v, dict) else {}

    @property
    def cover_dimension_histogram(self) -> list[dict]:
        raw = self.cover_art.get("dimensionHistogram")
        if not isinstance(raw, list):
            return []
        return [
            {"label": i["label"], "count": i["count"]}
            for i in raw
            if isinstance(i, dict)
            and isinstance(i.get("label"), str)
            and isinstance(i.get("count"), (int, float))
        ]

    def _ring(self, key: str) -> dict:
        v = self._d.get(key)
        if not isinstance(v, dict):
            return {"percent": 0, "complete": 0, "covered": 0, "total": 0}
        return v

    @property
    def tag_completeness(self) -> dict:
        return self._ring("tagCompleteness")

    @property
    def cover_coverage_800(self) -> dict:
        return self._ring("coverCoverage800")


def load_stats(path: Path | None = None) -> Stats:
    """Load and validate analysis-stats.json. Raises SchemaVersionError on
    mismatch, FileNotFoundError if analysis has never been run."""
    path = path or config.STATS_JSON
    data = _read_json(path)
    _check_schema(data, path, config.STATS_SCHEMA_VERSION)
    return Stats(data)


# ------------------------------------------------------------------- tracks


class TrackIndex:
    """In-memory index over tracks.json - loaded once, sliced per render.

    All filtering/search/pagination happens here so the UI only ever renders
    the current page (never ships all ~5,700 rows to the client).
    """

    def __init__(self, data: dict):
        self._d = data if isinstance(data, dict) else {}
        raw = self._d.get("tracks")
        self.tracks: list[dict] = [t for t in raw if isinstance(t, dict)] if isinstance(raw, list) else []

    @property
    def generated_at(self) -> str | None:
        v = self._d.get("generatedAt")
        return v if isinstance(v, str) else None

    @property
    def count(self) -> int:
        return len(self.tracks)

    def by_id(self, track_id: str) -> dict | None:
        for t in self.tracks:
            if t.get("id") == track_id:
                return t
        return None

    def genres(self, top: int = 10) -> list[str]:
        """Most common primary genres, for filter chips."""
        counts: dict[str, int] = {}
        for t in self.tracks:
            g = t.get("primaryGenre")
            if isinstance(g, str) and g and g != "Missing":
                counts[g] = counts.get(g, 0) + 1
        return [g for g, _ in sorted(counts.items(), key=lambda kv: -kv[1])[:top]]

    def decades(self) -> list[str]:
        seen: dict[str, int] = {}
        for t in self.tracks:
            d = t.get("decade")
            if isinstance(d, str) and d:
                seen[d] = seen.get(d, 0) + 1
        return sorted(seen.keys())

    def query(
        self,
        search: str = "",
        genre: str | None = None,
        decade: str | None = None,
        artist: str | None = None,
        page: int = 1,
        page_size: int = 50,
    ) -> tuple[list[dict], int]:
        """Filter + paginate. Returns (rows_for_page, total_matching)."""
        rows: list[dict] | Any = self.tracks
        if genre:
            rows = [t for t in rows if t.get("primaryGenre") == genre]
        if decade:
            rows = [t for t in rows if t.get("decade") == decade]
        if artist:
            rows = [t for t in rows if artist.lower() in str(t.get("artists", "")).lower()]
        if search:
            q = search.lower()
            rows = [
                t
                for t in rows
                if q in str(t.get("title", "")).lower()
                or q in str(t.get("artists", "")).lower()
                or q in str(t.get("album", "")).lower()
            ]
        total = len(rows)
        page = max(1, page)
        start = (page - 1) * page_size
        return rows[start : start + page_size], total


def load_tracks(path: Path | None = None) -> TrackIndex:
    """Load and validate tracks.json. Raises SchemaVersionError on mismatch."""
    path = path or config.TRACKS_JSON
    data = _read_json(path)
    _check_schema(data, path, config.TRACKS_SCHEMA_VERSION)
    return TrackIndex(data)


# ---------------------------------------------------- batch stats history

class StatsHistory:
    """GUI-owned cache of the stats summary per integration batch.

    Keyed by the batch boundary (an AudioMirror-derived batch date, the same
    canonical boundaries Recent Additions uses). Grows ~one row per batch.
    Lives in gui/.cache - NOT in AudioMirror, NOT in the library.
    """

    def __init__(self, path: Path | None = None):
        self.path = path or config.STATS_HISTORY_JSON
        self._data: dict[str, dict] = {}
        try:
            raw = _read_json(self.path)
            if isinstance(raw, dict) and isinstance(raw.get("batches"), dict):
                self._data = raw["batches"]
        except (OSError, json.JSONDecodeError):
            self._data = {}

    def record(self, batch_key: str, summary: dict) -> None:
        """Store (or refresh) the summary snapshot for the given batch."""
        if not batch_key or not isinstance(summary, dict):
            return
        self._data[batch_key] = summary
        self.path.parent.mkdir(parents=True, exist_ok=True)
        with open(self.path, "w", encoding="utf-8") as f:
            json.dump({"batches": self._data}, f, indent=2)

    def previous_summary(self, current_batch_key: str) -> dict | None:
        """The stored summary of the most recent batch BEFORE the given one."""
        older = sorted(k for k in self._data if k < current_batch_key)
        return self._data[older[-1]] if older else None


# ------------------------------------------------------------- formatters


def fmt_int(n: Any) -> str:
    try:
        return f"{int(n):,}"
    except (TypeError, ValueError):
        return "-"


def fmt_bytes(n: Any) -> str:
    try:
        n = float(n)
    except (TypeError, ValueError):
        return "-"
    for unit in ("B", "KB", "MB", "GB", "TB"):
        if n < 1024 or unit == "TB":
            return f"{n:,.2f} {unit}" if unit in ("GB", "TB") else f"{n:,.1f} {unit}"
        n /= 1024
    return "-"


def fmt_mmss(seconds: Any) -> str:
    try:
        s = int(round(float(seconds)))
    except (TypeError, ValueError):
        return "-"
    return f"{s // 60}m{s % 60:02d}s"


def relative_time(iso: str | None) -> str:
    """'2 hours ago' style rendering of the contract's ISO-8601 local times."""
    if not iso:
        return "never"
    from datetime import datetime

    try:
        then = datetime.fromisoformat(iso)
    except ValueError:
        return iso
    delta = datetime.now() - then
    secs = int(delta.total_seconds())
    if secs < 0:
        return "just now"
    if secs < 60:
        return "just now"
    if secs < 3600:
        return f"{secs // 60} min ago"
    if secs < 86400:
        h = secs // 3600
        return f"{h} hour{'s' if h != 1 else ''} ago"
    d = secs // 86400
    return f"{d} day{'s' if d != 1 else ''} ago"

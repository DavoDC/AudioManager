"""Unit tests for gui.data_loader - field mapping, pagination/filter
correctness, empty-library empty states, and loud schemaVersion failure."""
import json
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from gui import data_loader
from gui.data_loader import (
    SchemaVersionError,
    Stats,
    StatsHistory,
    TrackIndex,
    fmt_bytes,
    fmt_int,
    fmt_mmss,
    load_stats,
    load_tracks,
)

FIXTURES = Path(__file__).parent / "fixtures"


# ------------------------------------------------------------------ stats


def test_load_stats_summary_fields():
    s = load_stats(FIXTURES / "analysis-stats.json")
    assert s.summary_num("trackCount") == 4
    assert s.summary_num("artistCount") == 3
    assert s.summary_num("totalLibraryBytes") == 20971520
    assert s.summary_num("totalPlaybackHours") == 0.3
    assert s.generated_at == "2026-07-03T01:00:56"


def test_load_stats_distributions():
    s = load_stats(FIXTURES / "analysis-stats.json")
    assert s.genre_distribution[0] == {"label": "Hip Hop", "count": 2}
    assert len(s.decade_distribution) == 2
    assert s.age_distribution[0]["label"] == "0-2y"
    assert s.top_artists("all")[0]["label"] == "Akira The Don"
    assert s.top_artists("exclMusivation")[0]["label"] == "Eminem"
    assert s.cover_dimension_histogram[0]["label"] == "800x800"
    assert s.tag_completeness["percent"] == 75.0
    assert s.cover_coverage_800["covered"] == 3


def test_schema_version_mismatch_fails_loudly(tmp_path):
    bad = tmp_path / "analysis-stats.json"
    bad.write_text(json.dumps({"schemaVersion": 99, "summary": {}}), encoding="utf-8")
    with pytest.raises(SchemaVersionError) as e:
        load_stats(bad)
    assert "99" in str(e.value)


def test_schema_version_missing_fails_loudly(tmp_path):
    bad = tmp_path / "tracks.json"
    bad.write_text(json.dumps({"tracks": []}), encoding="utf-8")
    with pytest.raises(SchemaVersionError):
        load_tracks(bad)


def test_empty_library_returns_empty_states():
    s = Stats({"schemaVersion": 1, "summary": {}, "genreDistribution": [],
               "ageStats": {"averageYears": None}})
    assert s.summary_num("trackCount") == 0
    assert s.genre_distribution == []
    assert s.top_artists("all") == []
    assert s.tag_completeness["percent"] == 0
    assert s.cover_dimension_histogram == []


def test_malformed_distribution_entries_dropped():
    s = Stats({"genreDistribution": [
        {"label": "Rock", "count": 5},
        {"label": None, "count": 3},
        "garbage",
        {"count": 2},
    ]})
    assert s.genre_distribution == [{"label": "Rock", "count": 5}]


# ----------------------------------------------------------------- tracks


def test_load_tracks_field_mapping():
    idx = load_tracks(FIXTURES / "tracks.json")
    assert idx.count == 4
    t = idx.tracks[0]
    assert t["title"] == "Not Afraid"
    assert t["primaryArtist"] == "Eminem"
    assert t["compilation"] is False
    assert t["hiResArt"] is True
    assert idx.by_id("\\Artists\\Metallica\\Master of Puppets\\Battery.xml")["title"] == "Battery"


def test_query_search():
    idx = load_tracks(FIXTURES / "tracks.json")
    rows, total = idx.query(search="eminem")
    assert total == 2
    rows, total = idx.query(search="master of puppets")  # album match
    assert total == 1
    rows, total = idx.query(search="zzz-no-match")
    assert total == 0 and rows == []


def test_query_filters():
    idx = load_tracks(FIXTURES / "tracks.json")
    _, total = idx.query(genre="Hip Hop")
    assert total == 2
    _, total = idx.query(decade="1980s")
    assert total == 1
    _, total = idx.query(genre="Hip Hop", decade="1980s")
    assert total == 0
    _, total = idx.query(artist="plentakill")
    assert total == 1


def test_query_pagination():
    idx = load_tracks(FIXTURES / "tracks.json")
    rows, total = idx.query(page=1, page_size=3)
    assert total == 4 and len(rows) == 3
    rows, _ = idx.query(page=2, page_size=3)
    assert len(rows) == 1
    rows, _ = idx.query(page=99, page_size=3)
    assert rows == []
    rows, _ = idx.query(page=0, page_size=3)  # clamps to page 1
    assert len(rows) == 3


def test_track_index_empty_and_malformed():
    assert TrackIndex({}).count == 0
    assert TrackIndex({"tracks": "not-a-list"}).count == 0
    idx = TrackIndex({"tracks": [{"title": "ok"}, "garbage", None]})
    assert idx.count == 1
    assert idx.genres() == []
    assert idx.query(search="ok")[1] == 1


def test_genre_chips_ranked_by_count():
    idx = load_tracks(FIXTURES / "tracks.json")
    assert idx.genres()[0] == "Hip Hop"
    assert idx.decades() == ["1980s", "2020s"]


# ---------------------------------------------------------- stats history


def test_stats_history_roundtrip(tmp_path):
    h = StatsHistory(tmp_path / "stats-history.json")
    h.record("2026-06-06", {"trackCount": 5676, "totalLibraryBytes": 100})
    h.record("2026-06-28", {"trackCount": 5694, "totalLibraryBytes": 200})
    h2 = StatsHistory(tmp_path / "stats-history.json")
    prev = h2.previous_summary("2026-06-28")
    assert prev["trackCount"] == 5676
    assert h2.previous_summary("2026-06-06") is None


def test_stats_history_corrupt_file_recovers(tmp_path):
    p = tmp_path / "stats-history.json"
    p.write_text("{corrupt", encoding="utf-8")
    h = StatsHistory(p)
    assert h.previous_summary("2026-06-28") is None
    h.record("2026-06-28", {"trackCount": 1})  # can still write after corruption


# ------------------------------------------------------------- formatters


def test_formatters():
    assert fmt_int(5694) == "5,694"
    assert fmt_int(None) == "-"
    assert fmt_bytes(30558669644) == "28.46 GB"
    assert fmt_bytes(5366819) == "5.1 MB"
    assert fmt_bytes(None) == "-"
    assert fmt_mmss(228) == "3m48s"
    assert fmt_mmss(None) == "-"


def test_batch_path_to_id():
    from gui.batches import _mirror_path_to_track_id
    assert (
        _mirror_path_to_track_id("AUDIO_MIRROR/Artists/Eminem/Recovery/Not Afraid.xml")
        == "\\Artists\\Eminem\\Recovery\\Not Afraid.xml"
    )

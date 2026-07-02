"""Contract smoke test: assert every field the GUI reads is present in the
real C#-produced JSON files in logs/. Catches C#/Python schema drift the
moment it happens. Skips (rather than fails) if analysis has never run on
this machine - run `AudioManager.exe analysis --json-output --no-input` first.
"""
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from gui import config
from gui.data_loader import load_stats, load_tracks

STATS_FIELDS = [
    "trackCount", "artistCount", "genreCount", "totalLibraryBytes",
    "avgFileBytes", "totalPlaybackHours", "avgSongLengthSeconds",
    "medianSongLengthSeconds",
]
TRACK_FIELDS = [
    "id", "title", "artists", "primaryArtist", "album", "year", "decade",
    "genres", "primaryGenre", "trackNumber", "lengthSeconds", "length",
    "compilation", "coverWidth", "coverHeight", "hasArt", "hiResArt",
    "addedDate", "filePath",
]


@pytest.mark.skipif(not config.STATS_JSON.exists(), reason="analysis has not been run")
def test_real_stats_json_has_every_consumed_field():
    s = load_stats()
    for f in STATS_FIELDS:
        assert f in s.summary, f"summary.{f} missing from real analysis-stats.json"
    for key in ("genreDistribution", "decadeDistribution", "yearDistribution",
                "ageDistribution"):
        dist = s._dist(key)
        assert dist, f"{key} empty/missing in real analysis-stats.json"
    assert s.top_artists("all") and s.top_artists("exclMusivation")
    assert s.cover_dimension_histogram
    assert "percent" in s.tag_completeness
    assert "percent" in s.cover_coverage_800
    assert s.age_stats.get("averageYears") is not None


@pytest.mark.skipif(not config.TRACKS_JSON.exists(), reason="analysis has not been run")
def test_real_tracks_json_has_every_consumed_field():
    idx = load_tracks()
    assert idx.count > 0
    first = idx.tracks[0]
    for f in TRACK_FIELDS:
        assert f in first, f"track field '{f}' missing from real tracks.json"

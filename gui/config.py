"""Central paths and settings for the AudioManager GUI.

The GUI consumes the two C#-emitted JSON contracts (analysis-stats.json,
tracks.json) and triggers the exe via subprocess. It never parses AudioMirror
XML and never writes to the library, NewMusic, or AudioMirror.
"""
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
GUI_ROOT = REPO_ROOT / "gui"

EXE_PATH = REPO_ROOT / "project" / "AudioManager" / "bin" / "Release" / "AudioManager.exe"
LOGS_DIR = REPO_ROOT / "logs"
STATS_JSON = LOGS_DIR / "analysis-stats.json"
TRACKS_JSON = LOGS_DIR / "tracks.json"

AUDIOMIRROR_REPO = REPO_ROOT.parent / "AudioMirror"
SPOTIFYGEN_ROOT = REPO_ROOT.parent / "SpotifyTools"

# NewMusic staging inbox (matches Constants.cs). The GUI only READS files
# here (album-art extraction for the review queue) - it never moves,
# renames or deletes anything in it; only the exe's own integrate mode does.
NEWMUSIC_DIR = Path(r"C:\Users\David\Downloads\NewMusic")

CACHE_DIR = GUI_ROOT / ".cache"
THUMBS_DIR = CACHE_DIR / "thumbs"
STATS_HISTORY_JSON = CACHE_DIR / "stats-history.json"
ACQUIRE_STATE_JSON = CACHE_DIR / "acquire-state.json"

# Schema versions this GUI was built against (see docs/References/AnalysisJson-Format.md)
STATS_SCHEMA_VERSION = 1
TRACKS_SCHEMA_VERSION = 1

# Subprocess timeouts (seconds)
TIMEOUT_ANALYSIS = 600
TIMEOUT_DRY_RUN = 600
TIMEOUT_FORCE_REGEN = 3600
TIMEOUT_INTEGRATE = 1800
TIMEOUT_TAGFIX = 600

PAGE_SIZE_TABLE = 50
PAGE_SIZE_GRID = 24

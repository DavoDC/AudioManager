"""The ONLY touchpoint between AudioManager and the SpotifyTools kit.

Nothing else under gui/ may import spotify_tools (enforced by
gui/tests/test_spotify_bridge.py). Names are re-exported lazily (PEP 562):
`from gui.spotify_bridge import normalise` inside a function resolves the kit
at call time, so the app starts without SpotifyTools installed and every
lookup is fresh (test monkeypatches on the kit modules keep working).

Kit location: the SPOTIFY_TOOLS_ROOT env var, else a sibling checkout named
SpotifyTools (see gui.config.SPOTIFYGEN_ROOT). Needs API_VERSION >= 1.
"""
from __future__ import annotations

import importlib
import sys

from gui import config

MIN_API_VERSION = 1

# public name -> kit submodule that holds it ("" = package root)
_EXPORTS = {
    "API_VERSION": "",
    "CONFIG_PATH": "config",
    "load_config": "config",
    "RealSpotifyClient": "spotify_client",
    "move_liked_to_playlist": "acquire",
    "extract_playlist_id": "open_playlist",
    "build_deemix_url": "open_playlist",
    "clean_artist": "matcher",
    "clean_title": "matcher",
    "normalise": "matcher",
}
__all__ = ["SpotifyToolsMissing", "SpotifyToolsTooOld", "require", *_EXPORTS]


class SpotifyToolsMissing(ImportError):
    """SpotifyTools could not be imported from the expected location."""


class SpotifyToolsTooOld(SpotifyToolsMissing):
    """SpotifyTools is present but its API_VERSION is below MIN_API_VERSION."""


def _src_path() -> str:
    return str(config.SPOTIFYGEN_ROOT / "src")


def require():
    """Put the kit on sys.path (once), import it, check API_VERSION; return the package."""
    src = _src_path()
    if src not in sys.path:
        sys.path.insert(0, src)
    try:
        kit = importlib.import_module("spotify_tools")
    except ImportError as exc:
        raise SpotifyToolsMissing(
            f"SpotifyTools not found (tried {src}). Check out SpotifyTools there "
            f"or set the SPOTIFY_TOOLS_ROOT environment variable to its folder."
        ) from exc
    if getattr(kit, "API_VERSION", 0) < MIN_API_VERSION:
        raise SpotifyToolsTooOld(
            f"SpotifyTools at {src} is too old for AudioManager "
            f"(needs API_VERSION >= {MIN_API_VERSION}). Update that checkout, "
            f"or point SPOTIFY_TOOLS_ROOT at a newer one.")
    return kit


def __getattr__(name: str):
    if name not in _EXPORTS:
        raise AttributeError(f"module {__name__!r} has no attribute {name!r}")
    kit = require()
    sub = _EXPORTS[name]
    holder = importlib.import_module(f"spotify_tools.{sub}") if sub else kit
    return getattr(holder, name)

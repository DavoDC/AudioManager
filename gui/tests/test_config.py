"""Tests for gui.config path resolution.

Regression coverage for SpotifyTools docs/IDEAS.md Task 2 (closed 2026-09-04):
SPOTIFYGEN_ROOT was a bare hardcoded sibling-folder path with no override, the
AudioManager-side half of the same fragility that caused SpotifyTools's own
CONFIG_PATH/CACHE_PATH regression (see spotify_tools/paths.py). It now checks
the SPOTIFY_TOOLS_ROOT env var first - the same variable name SpotifyTools's
own paths.py checks - so one variable controls both sides of the sibling
sys.path.insert boundary (gui/tabs/acquire.py, gui/tests/test_acquire.py).
"""
import importlib
import os
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from gui import config as config_module


def test_spotifygen_root_defaults_to_sibling_folder():
    """No env var set: falls back to <repo-parent>/SpotifyTools."""
    assert config_module.SPOTIFYGEN_ROOT == config_module.REPO_ROOT.parent / "SpotifyTools"


def test_spotifygen_root_env_var_overrides_default(tmp_path):
    """SPOTIFY_TOOLS_ROOT, if set, wins over the sibling-folder default.

    Uses os.environ directly (not monkeypatch) so the env var is guaranteed
    gone BEFORE the restoring reload runs - mirrors the fix applied to
    SpotifyTools's own tests/unit/test_paths.py for the identical hazard:
    monkeypatch only reverts in fixture teardown, which runs AFTER this
    function returns, so a `finally: importlib.reload(...)` would still see
    the env var set and re-poison the module for every test that follows.
    """
    original = config_module.SPOTIFYGEN_ROOT
    os.environ["SPOTIFY_TOOLS_ROOT"] = str(tmp_path)
    try:
        importlib.reload(config_module)
        assert config_module.SPOTIFYGEN_ROOT == Path(str(tmp_path))
    finally:
        del os.environ["SPOTIFY_TOOLS_ROOT"]
        importlib.reload(config_module)
        assert config_module.SPOTIFYGEN_ROOT == original

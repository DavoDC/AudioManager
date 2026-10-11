"""Boundary tests: gui/spotify_bridge.py is the only touchpoint with SpotifyTools."""
import re
import sys
import types
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from gui import config, spotify_bridge

GUI = Path(__file__).resolve().parents[1]
_IMPORT = re.compile(r"^\s*(from\s+spotify_tools\b|import\s+spotify_tools\b)", re.M)


def _non_test_sources():
    for p in GUI.rglob("*.py"):
        rel = p.relative_to(GUI).parts
        if p.name != "spotify_bridge.py" and "tests" not in rel and ".cache" not in rel:
            yield p


def test_only_the_bridge_imports_spotify_tools():
    offenders = [str(p.relative_to(GUI)) for p in _non_test_sources()
                 if _IMPORT.search(p.read_text(encoding="utf-8"))]
    assert offenders == []


def test_only_the_bridge_inserts_the_kit_sys_path():
    offenders = []
    for p in _non_test_sources():
        text = p.read_text(encoding="utf-8")
        if re.search(r"sys.path.(insert|append).*SPOTIFYGEN_ROOT", text):
            offenders.append(str(p.relative_to(GUI)))
    assert offenders == []


def test_missing_kit_error_names_path_and_env_var(monkeypatch, tmp_path):
    monkeypatch.setattr(config, "SPOTIFYGEN_ROOT", tmp_path / "nokit")
    for name in [m for m in sys.modules if m == "spotify_tools" or m.startswith("spotify_tools.")]:
        monkeypatch.delitem(sys.modules, name)
    monkeypatch.setattr(sys, "path", [p for p in sys.path if "SpotifyTools" not in p])
    with pytest.raises(spotify_bridge.SpotifyToolsMissing) as exc:
        spotify_bridge.require()
    assert str(tmp_path / "nokit") in str(exc.value)
    assert "SPOTIFY_TOOLS_ROOT" in str(exc.value)


def test_old_api_version_is_rejected(monkeypatch):
    monkeypatch.setitem(sys.modules, "spotify_tools", types.SimpleNamespace(API_VERSION=0))
    with pytest.raises(spotify_bridge.SpotifyToolsTooOld) as exc:
        spotify_bridge.require()
    assert "API_VERSION >= 1" in str(exc.value)


def test_missing_api_version_is_rejected(monkeypatch):
    monkeypatch.setitem(sys.modules, "spotify_tools", types.SimpleNamespace())
    with pytest.raises(spotify_bridge.SpotifyToolsTooOld):
        spotify_bridge.require()


def test_current_api_version_passes_and_names_resolve(monkeypatch):
    fake = types.SimpleNamespace(API_VERSION=1)
    monkeypatch.setitem(sys.modules, "spotify_tools", fake)
    assert spotify_bridge.require() is fake
    assert spotify_bridge.API_VERSION == 1


def test_unknown_name_is_attribute_error():
    with pytest.raises(AttributeError):
        spotify_bridge.not_a_kit_name

"""WS-009: the port constant equals the registry entry, and every place that
hard-codes the port agrees (a drift test is the only thing keeping it true)."""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from gui import security
from gui.main import parse_args

ROOT = Path(__file__).resolve().parents[2]
REGISTRY = json.loads((ROOT / "config" / "port-registry.json").read_text(encoding="utf-8"))


def test_registry_entry_is_this_app():
    assert REGISTRY["app"] == "AudioManager"


def test_security_default_port_equals_registry():
    assert security.DEFAULT_PORT == REGISTRY["port"]


def test_main_default_port_equals_registry(monkeypatch):
    monkeypatch.delenv("AM_GUI_PORT", raising=False)
    assert parse_args([]).port == REGISTRY["port"]


def test_documented_urls_use_the_registry_port():
    for rel in ("gui/README.md", "docs/References/GUI-State-Contract.md"):
        text = (ROOT / rel).read_text(encoding="utf-8")
        assert f"localhost:{REGISTRY['port']}" in text, rel


def test_gui_bat_writes_a_launch_breadcrumb_line(tmp_path):
    """WS-013: run the bat's own breadcrumb lines in a scratch cwd; each run appends one line."""
    import subprocess
    lines = (ROOT / "scripts" / "Gui.bat").read_text(encoding="utf-8").splitlines()
    start = next(i for i, ln in enumerate(lines) if "launch breadcrumb" in ln)
    snippet = [ln for ln in lines[start + 1:start + 4] if ln.strip()]
    assert len(snippet) == 2, snippet
    bat = tmp_path / "crumb.bat"
    bat.write_text("@echo off\r\nset KILLED=0\r\n" + "\r\n".join(snippet) + "\r\n", encoding="utf-8")
    for _ in range(2):
        subprocess.run(["cmd", "/c", str(bat)], cwd=tmp_path, check=True, timeout=30)
    crumbs = (tmp_path / "gui" / ".cache" / "launch-breadcrumb.log").read_text(encoding="utf-8").splitlines()
    assert len(crumbs) == 2 and all("Gui.bat launch" in c for c in crumbs)

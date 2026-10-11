"""WS-121: the GUI makes no external requests. Fonts are bundled; no http(s) URL may be loaded
by markup, CSS or script the GUI serves (text links in prose are fine: only src/href/url()/@import count)."""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from gui import theme

GUI = Path(__file__).resolve().parents[1]
LOADERS = re.compile(r"""(?:src|href)\s*=\s*["']https?://|url\(\s*["']?https?://|@import\s+(?:url\()?["']?https?://""")


def test_head_html_loads_nothing_external():
    assert not LOADERS.search(theme.HEAD_HTML)
    assert "googleapis" not in theme.HEAD_HTML and "gstatic" not in theme.HEAD_HTML


def test_no_python_source_embeds_an_external_loader():
    offenders = []
    for path in list(GUI.rglob("*.py")):
        if "tests" in path.parts or "__pycache__" in path.parts:
            continue
        if LOADERS.search(path.read_text(encoding="utf-8")):
            offenders.append(str(path.relative_to(GUI)))
    assert offenders == []


def test_bundled_fonts_exist_and_are_declared():
    fonts = GUI / "static" / "fonts"
    for weight in (400, 500, 600, 700):
        assert (fonts / f"ibm-plex-sans-latin-{weight}-normal.woff2").stat().st_size > 1000
        assert (fonts / f"ibm-plex-mono-latin-{weight}-normal.woff2").stat().st_size > 1000
        assert f"ibm-plex-sans-latin-{weight}-normal.woff2" in theme.HEAD_HTML
    assert (fonts / "OFL.txt").exists()

"""State favicon (WS-124): one icon renderer, colour follows the health verdict."""
import io
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))

from PIL import Image

from gui import icon, tokens


def _centre(verdict):
    img = icon.render(verdict, 64)
    return img.getpixel((32, 32))[:3]


def _rgb(hexstr):
    h = hexstr.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def test_icon_colour_follows_the_verdict():
    assert _centre("green") == _rgb(tokens.HEALTH_GREEN)
    assert _centre("amber") == _rgb(tokens.HEALTH_AMBER)
    assert _centre("red") == _rgb(tokens.HEALTH_RED)


def test_unknown_verdict_renders_neutral_not_an_error():
    assert _centre("nonsense") == _rgb(tokens.TEXT_DIM)


def test_png_bytes_are_a_valid_square_image():
    data = icon.png_bytes("green", 32)
    img = Image.open(io.BytesIO(data))
    assert img.format == "PNG" and img.size == (32, 32)


def test_favicon_route_serves_png_for_the_live_verdict():
    from fastapi import FastAPI
    from fastapi.testclient import TestClient
    from gui import api
    app = FastAPI()
    api.register(app)
    r = TestClient(app).get("/api/favicon.png")
    assert r.status_code == 200 and r.headers["content-type"] == "image/png"
    assert Image.open(io.BytesIO(r.content)).size == (64, 64)
    assert r.headers["cache-control"] == "no-store"


def test_page_head_links_the_icon_with_the_verdict():
    from gui import chrome
    head = chrome.head_html()
    assert 'rel="icon"' in head and "/api/favicon.png?v=" in head and 'data-testid="favicon"' in head


def test_ui_check_flags_a_page_without_the_favicon_link():
    from gui import ui_check
    assert ui_check.favicon_problems('<link rel="icon" href="/api/favicon.png?v=green" data-testid="favicon">') == []
    assert ui_check.favicon_problems("<html></html>")

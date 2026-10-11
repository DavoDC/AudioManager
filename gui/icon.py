"""One icon renderer for every surface that shows health as a colour (WS-011, WS-124).

The browser favicon (/api/favicon.png) and the tray icon both call render(), so they
cannot drift. Colours come from gui/tokens.py; the verdict comes from gui/health.py.
"""
from __future__ import annotations

import io

from PIL import Image, ImageDraw

from gui import tokens

VERDICT_COLOURS = {"green": tokens.HEALTH_GREEN, "amber": tokens.HEALTH_AMBER, "red": tokens.HEALTH_RED}


def _rgb(hexstr: str) -> tuple[int, int, int]:
    h = hexstr.lstrip("#")
    return int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16)


def render(verdict: str, size: int = 64) -> Image.Image:
    """Dark rounded tile with a filled disc in the verdict colour (neutral for unknown)."""
    colour = _rgb(VERDICT_COLOURS.get(verdict, tokens.TEXT_DIM))
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((0, 0, size - 1, size - 1), radius=size // 5, fill=_rgb(tokens.BG) + (255,))
    pad = size // 6
    d.ellipse((pad, pad, size - 1 - pad, size - 1 - pad), fill=colour + (255,))
    return img


def png_bytes(verdict: str, size: int = 64) -> bytes:
    buf = io.BytesIO()
    render(verdict, size).save(buf, format="PNG")
    return buf.getvalue()

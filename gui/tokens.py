"""Design tokens: every colour in the GUI, defined once (WS-111).

Nothing else under gui/ may hold a hex colour (tests/test_tokens.py lints it). CSS strings use
@@NAME@@ placeholders that fill() swaps for these values at import time.
"""
import re

ACCENT = "#5b8cff"
ACCENT2 = "#7fd1ae"
ACCENT3 = "#f2b84b"
ACCENT4 = "#e26d6d"
ACCENT5 = "#b98af0"
BG = "#14161c"
BORDER_HOVER = "#3a4154"
CONTROL_BG = "#12141c"
ERROR_BANNER_BG = "#7a1f1f"
ERROR_BORDER = "#c0392b"
FIXTURE_BG = "#5a4a00"
HEALTH_AMBER = "#e0a030"
HEALTH_GREEN = "#5fb878"
HEALTH_RED = "#e05a4a"
NAV_ACTIVE_BG = "#181b24"
NAV_BG = "#10121a"
ON_ACCENT = "#0c0e13"
PANEL = "#1c1f28"
PANEL_BORDER = "#2a2e3a"
STEP_ACTIVE_BG = "#181b2e"
TEXT = "#e6e8ee"
TEXT_DIM = "#9aa0ac"
TRACK_MISSING = "#565c6e"
WHITE = "#fff"
HUE_1F4A7A = "#1f4a7a"
HUE_1F5C48 = "#1f5c48"
HUE_5A3A8A = "#5a3a8a"
HUE_5BB8FF = "#5bb8ff"
HUE_5BD0FF = "#5bd0ff"
HUE_5C521F = "#5c521f"
HUE_5C6270 = "#5c6270"
HUE_6D7BFF = "#6d7bff"
HUE_7A2D2D = "#7a2d2d"
HUE_7A2D5A = "#7a2d5a"
HUE_7A3F1F = "#7a3f1f"
HUE_7A9F6A = "#7a9f6a"
HUE_8A5A10 = "#8a5a10"
HUE_8A7FF0 = "#8a7ff0"
HUE_CFE0FF = "#cfe0ff"
HUE_D1C37F = "#d1c37f"
HUE_E2585B = "#e2585b"
HUE_E26DB8 = "#e26db8"
HUE_E2905B = "#e2905b"
HUE_E2A05B = "#e2a05b"
SURFACE_0E1017 = "#0e1017"
SURFACE_151823 = "#151823"
SURFACE_23262E = "#23262e"
SURFACE_243A7A = "#243a7a"
SURFACE_262B38 = "#262b38"
SURFACE_3A2F8A = "#3a2f8a"
SURFACE_7A1F22 = "#7a1f22"
SURFACE_3A3F4D = "#3a3f4d"

CSS_VARS = {"--bg": BG, "--panel": PANEL, "--panel-border": PANEL_BORDER, "--text": TEXT,
            "--text-dim": TEXT_DIM, "--accent": ACCENT, "--accent2": ACCENT2,
            "--accent3": ACCENT3, "--accent4": ACCENT4, "--accent5": ACCENT5}

_PLACEHOLDER = re.compile(r"@@([A-Z0-9_]+)@@")

def fill(css: str) -> str:
    """Swap every @@NAME@@ placeholder for its token value; an unknown name is a bug."""
    return _PLACEHOLDER.sub(lambda m: globals()[m.group(1)], css)

"""Design tokens (WS-111): every colour is defined once in gui/tokens.py."""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))

from gui import tokens

# A hex colour; (?<!&) skips HTML entities such as &#10003;
HEX = re.compile(r"(?<![&\w])#(?:[0-9a-fA-F]{8}|[0-9a-fA-F]{6}|[0-9a-fA-F]{3})\b(?![-\w])")
LINT_SKIP = {"tokens.py"}
LINT_DIRS_SKIP = {"tests", "static", ".cache", "__pycache__", "fixtures"}


def _gui_sources():
    for p in (ROOT / "gui").rglob("*.py"):
        parts = set(p.relative_to(ROOT / "gui").parts)
        if p.name in LINT_SKIP or parts & LINT_DIRS_SKIP:
            continue
        yield p


def test_no_hex_colour_outside_tokens():
    hits = []
    for p in _gui_sources():
        for n, line in enumerate(p.read_text(encoding="utf-8").splitlines(), 1):
            for m in HEX.finditer(line):
                hits.append(f"{p.relative_to(ROOT).as_posix()}:{n}: {m.group(0)}")
    assert not hits, "hex colours outside gui/tokens.py:\n" + "\n".join(hits)


def test_every_token_is_a_hex_colour_and_unique_by_name():
    names = [n for n in vars(tokens) if n.isupper() and n not in {"CSS_VARS"} and not n.startswith("_")]
    assert names
    for n in names:
        value = getattr(tokens, n)
        assert isinstance(value, str) and HEX.fullmatch(value), n


def test_fill_replaces_placeholders_and_rejects_unknown():
    assert tokens.fill("a:@@BG@@;") == f"a:{tokens.BG};"
    try:
        tokens.fill("@@NOPE@@")
    except KeyError:
        return
    raise AssertionError("unknown placeholder must raise")


def test_design_md_palette_matches_tokens():
    text = (ROOT / "docs" / "DESIGN.md").read_text(encoding="utf-8")
    rows = dict(re.findall(r"\|\s*`(--[a-z0-9-]+)`\s*\|\s*`(#[0-9a-fA-F]{6})`", text))
    assert rows, "palette table not found in docs/DESIGN.md"
    for var, value in tokens.CSS_VARS.items():
        assert rows.get(var, "").lower() == value.lower(), f"{var}: DESIGN.md {rows.get(var)} vs tokens {value}"


def test_no_unused_placeholders_left_in_rendered_css():
    from gui import chrome, theme
    assert "@@" not in theme.HEAD_HTML and "@@" not in chrome.CSS

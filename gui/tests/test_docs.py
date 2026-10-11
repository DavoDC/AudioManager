"""Docs are contracts (WS-145): paths and links named in the docs must exist."""
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DOC_PATH = re.compile(r"`((?:docs|scripts|gui)/[A-Za-z0-9_./-]+\.[A-Za-z0-9]+)`")
MD_LINK = re.compile(r"\]\((?!https?:|#|mailto:)([^)#\s]+)")
SKIP_TREES = ("docs/Historical", "docs/Development")


def _md_files():
    files = [ROOT / "CLAUDE.md", ROOT / "README.md"]
    for f in (ROOT / "docs").rglob("*.md"):
        rel = f.relative_to(ROOT).as_posix()
        if not rel.startswith(SKIP_TREES):
            files.append(f)
    return [f for f in files if f.exists()]


def test_claude_md_named_paths_exist():
    text = (ROOT / "CLAUDE.md").read_text(encoding="utf-8")
    missing = [p for p in DOC_PATH.findall(text)
               if not any(c in p for c in "*<>") and "/.cache/" not in p and not (ROOT / p).exists()]
    assert not missing, f"CLAUDE.md names paths that do not exist: {missing}"


def test_relative_markdown_links_resolve():
    broken = []
    for f in _md_files():
        for target in MD_LINK.findall(f.read_text(encoding="utf-8")):
            if not (f.parent / target).resolve().exists():
                broken.append(f"{f.relative_to(ROOT).as_posix()} -> {target}")
    assert not broken, "broken relative links:\n" + "\n".join(broken)


def test_claude_md_within_line_budget():
    lines = (ROOT / "CLAUDE.md").read_text(encoding="utf-8").splitlines()
    assert len(lines) <= 150, f"CLAUDE.md has {len(lines)} lines, limit 150"


def test_state_contract_has_verification_ladder():
    text = (ROOT / "docs/References/GUI-State-Contract.md").read_text(encoding="utf-8")
    assert "Verification ladder" in text
    for rung in ("unit test", "verify", "DOM text", "capture", "browser tool"):
        assert rung in text, rung

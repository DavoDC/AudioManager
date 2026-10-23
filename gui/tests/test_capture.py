"""capture.verify_dom: the pass/fail rules for one rendered visit."""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from gui import capture

GOOD = (
    '<html data-hscroll="no" data-content-len="120"><body>'
    '<button id="nav-stats" data-testid="nav-stats" role="tab" aria-selected="true">Statistics</button>'
    '<div id="error-banner" role="alert" hidden=""></div>'
    '<div id="build-footer" data-testid="build-footer">AudioManager abc1234 2026-10-12</div>'
    "</body></html>"
)


def test_default_widths_are_desktop_only():
    # The GUI is desktop-only (David, 2026-10-23): no phone width in the default matrix.
    assert capture.WIDTHS == (1280,)


def test_good_page_passes():
    assert capture.verify_dom("stats", GOOD, "", 390) == []


def test_app_error_in_console_fails():
    p = capture.verify_dom("stats", GOOD, "[app-error] error: boom", 1280)
    assert any("app-error" in x for x in p)


def test_visible_banner_fails():
    dom = GOOD.replace(' hidden=""', "")
    assert any("banner is visible" in x for x in capture.verify_dom("stats", dom, "", 1280))


def test_missing_footer_unselected_tab_empty_and_overflow():
    dom = (GOOD.replace('aria-selected="true"', 'aria-selected="false"')
           .replace("AudioManager abc1234", "nothing")
           .replace('data-content-len="120"', 'data-content-len="0"')
           .replace('data-hscroll="no"', 'data-hscroll="yes"'))
    p = " | ".join(capture.verify_dom("stats", dom, "", 390))
    assert "footer" in p and "not selected" in p and "empty" in p and "horizontally" in p


def test_unknown_build_footer_still_counts():
    dom = GOOD.replace("abc1234 2026-10-12", "unknown")
    assert capture.verify_dom("stats", dom, "", 1280) == []


def test_fixture_banner_required_for_demo_scenarios():
    assert any("FIXTURE" in x for x in capture.verify_dom("stats", GOOD, "", 1280, fixture="empty"))
    dom = GOOD.replace("</body>", '<div data-testid="fixture-banner">FIXTURE empty</div></body>')
    assert capture.verify_dom("stats", dom, "", 1280, fixture="empty") == []

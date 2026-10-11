"""One health verdict (WS-052): green/amber/red plus reasons, shared by every surface."""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from gui import health


def _v(**kw):
    base = dict(exe_ok=True, data_ok=True, data_error=None, recent_errors=0, fixture=None)
    base.update(kw)
    return health.verdict(**base)


def test_all_good_is_green():
    v = _v()
    assert v["verdict"] == "green" and v["reasons"] == []


def test_missing_exe_is_red():
    v = _v(exe_ok=False)
    assert v["verdict"] == "red" and any("exe" in r for r in v["reasons"])


def test_missing_data_is_amber_with_the_load_error():
    v = _v(data_ok=False, data_error="analysis-stats.json not found")
    assert v["verdict"] == "amber" and "analysis-stats.json not found" in v["reasons"]


def test_recent_browser_errors_are_amber():
    v = _v(recent_errors=2)
    assert v["verdict"] == "amber" and any("2" in r for r in v["reasons"])


def test_red_outranks_amber():
    assert _v(exe_ok=False, data_ok=False)["verdict"] == "red"


def test_fixture_mode_is_amber_not_red_without_exe():
    v = _v(exe_ok=False, fixture="review")
    assert v["verdict"] == "amber" and any("fixture" in r.lower() for r in v["reasons"])


def test_word_matches_verdict():
    assert health.word("green") == "Healthy"
    assert health.word("amber") == "Attention"
    assert health.word("red") == "Broken"


def test_compute_reads_live_state_and_never_raises():
    v = health.compute()
    assert v["verdict"] in {"green", "amber", "red"} and isinstance(v["reasons"], list)


def test_api_health_endpoint_and_manifest():
    from fastapi import FastAPI
    from fastapi.testclient import TestClient
    from gui import api
    app = FastAPI()
    api.register(app)
    c = TestClient(app)
    r = c.get("/api/health")
    body = r.json()
    assert r.status_code == 200 and body["verdict"] in {"green", "amber", "red"}
    assert "reasons" in body and body["word"] == health.word(body["verdict"])
    assert any(e["path"] == "/api/health" for e in api.ENDPOINTS)


def test_footer_shows_the_same_word():
    from gui import chrome
    html = chrome.body_html()
    assert 'data-testid="health-badge"' in html

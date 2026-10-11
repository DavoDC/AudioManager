"""State history ring buffer (WS-064): last N snapshots with a changes-since cursor."""
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))

from gui import history


def _state(rows_a=1, busy=False, action=None):
    return {"busy": {"busy": busy, "action": action},
            "tabs": {"stats": {"row_count": rows_a, "rows": [{"testid": f"s{i}"} for i in range(rows_a)]},
                     "library": {"row_count": 0, "rows": []}}}


def test_record_appends_a_snapshot_with_cursor_and_summary():
    h = history.History(size=5)
    snap = h.record(_state(rows_a=2), verdict="green")
    assert snap["seq"] == 1 and snap["verdict"] == "green" and snap["at"]
    assert snap["tabs"]["stats"]["row_count"] == 2
    assert set(snap["tabs"]["stats"]) >= {"row_count", "digest"}


def test_identical_state_is_not_recorded_twice():
    h = history.History(size=5)
    h.record(_state(), verdict="green")
    assert h.record(_state(), verdict="green") is None
    assert len(h.since(0)) == 1


def test_a_changed_row_or_verdict_or_busy_flag_is_a_new_snapshot():
    h = history.History(size=5)
    h.record(_state(rows_a=1), verdict="green")
    assert h.record(_state(rows_a=2), verdict="green")
    assert h.record(_state(rows_a=2), verdict="red")
    assert h.record(_state(rows_a=2, busy=True, action="analysis"), verdict="red")
    assert [s["seq"] for s in h.since(0)] == [1, 2, 3, 4]


def test_ring_buffer_drops_the_oldest_beyond_size():
    h = history.History(size=3)
    for n in range(1, 7):
        h.record(_state(rows_a=n), verdict="green")
    seqs = [s["seq"] for s in h.since(0)]
    assert seqs == [4, 5, 6]


def test_since_returns_only_newer_snapshots():
    h = history.History(size=10)
    for n in range(1, 5):
        h.record(_state(rows_a=n), verdict="green")
    assert [s["seq"] for s in h.since(2)] == [3, 4]
    assert h.since(4) == []


def test_payload_has_cursor_and_size():
    h = history.History(size=3)
    h.record(_state(), verdict="green")
    p = h.payload(0)
    assert p["cursor"] == 1 and p["size"] == 3 and len(p["snapshots"]) == 1
    assert h.payload(1)["snapshots"] == [] and h.payload(1)["cursor"] == 1


def test_endpoint_returns_snapshots_and_honours_since():
    from fastapi import FastAPI
    from fastapi.testclient import TestClient
    from gui import api
    app = FastAPI()
    api.register(app)
    c = TestClient(app)
    first = c.get("/state/history.json").json()
    assert first["snapshots"] and first["cursor"] >= 1
    later = c.get(f"/state/history.json?since={first['cursor']}").json()
    assert later["snapshots"] == [] and later["cursor"] == first["cursor"]
    assert any(e["path"] == "/state/history.json" for e in api.ENDPOINTS)


def test_history_route_is_not_swallowed_by_the_tab_route():
    from fastapi import FastAPI
    from fastapi.testclient import TestClient
    from gui import api
    app = FastAPI()
    api.register(app)
    assert "snapshots" in TestClient(app).get("/state/history.json").json()

"""OPS-4 (WS-075): small text baselines per fixture, tolerance diff that names the region."""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from gui import baseline


def test_normalize_drops_volatile_keys_and_truncates_rows():
    doc = {"tabs": {"stats": {"generated_at": "now", "row_count": 9,
                              "rows": [{"testid": f"r{i}", "value": i} for i in range(9)]}}}
    n = baseline.normalize(doc)
    assert "generated_at" not in n["tabs"]["stats"]
    assert len(n["tabs"]["stats"]["rows"]) == baseline.MAX_ROWS
    assert n["tabs"]["stats"]["row_count"] == 9


def test_diff_names_the_changed_region():
    old = {"tabs": {"stats": {"rows": [{"testid": "stat-a", "value": 4}]}}}
    new = {"tabs": {"stats": {"rows": [{"testid": "stat-a", "value": 5}]}}}
    out = baseline.diff(old, new)
    assert len(out) == 1
    assert "stats" in out[0] and "stat-a" in out[0] and "value" in out[0] and "4" in out[0] and "5" in out[0]


def test_diff_tolerates_small_numeric_drift_but_not_large():
    old = {"tabs": {"t": {"x": 100.0}}}
    assert baseline.diff(old, {"tabs": {"t": {"x": 100.5}}}) == []
    assert baseline.diff(old, {"tabs": {"t": {"x": 120.0}}}) != []


def test_diff_reports_added_and_removed_rows():
    old = {"tabs": {"t": {"rows": [{"testid": "a"}]}}}
    new = {"tabs": {"t": {"rows": [{"testid": "b"}]}}}
    out = " ".join(baseline.diff(old, new))
    assert "a" in out and "b" in out


def test_committed_baselines_match_the_fixture_servers():
    # Real fixture servers, committed baselines: exit 0 when nothing changed.
    assert baseline.main(["check", "--fixture", "normal", "--fixture", "review"]) == 0


def test_check_exits_nonzero_naming_the_region_when_a_baseline_differs(tmp_path, capsys):
    bad = tmp_path / "normal.json"
    doc = json.loads((baseline.BASELINES / "normal.json").read_text(encoding="utf-8"))
    doc["tabs"]["stats"]["rows"][0]["value"] = 999999
    bad.write_text(json.dumps(doc), encoding="utf-8")
    code = baseline.main(["check", "--fixture", "normal", "--dir", str(tmp_path)])
    out = capsys.readouterr().out
    assert code == 1
    assert "stats" in out and "999999" in out

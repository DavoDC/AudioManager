"""ui_check pure helpers: painted test ids vs state rows."""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from gui import ui_check


def test_extract_handles_plain_and_json_escaped():
    src = 'x data-testid="row-a-1" y data-testid=\\"row-b-2\\" z "testid":"row-c-3"'
    assert ui_check.extract_testids(src, "row-") == {"row-a-1", "row-b-2", "row-c-3"}


def test_extract_filters_by_prefix():
    assert ui_check.extract_testids('data-testid="nav-x" data-testid="row-q-1"', "row-") == {"row-q-1"}


def test_compare_exact_reports_missing_and_extra():
    r = ui_check.compare({"a", "b"}, {"b", "c"}, exact=True)
    assert r == {"missing": ["a"], "unexpected": ["c"]}


def test_compare_subset_allows_unpainted_state_rows():
    r = ui_check.compare({"a", "b"}, {"a"}, exact=False)
    assert r == {"missing": [], "unexpected": []}
    assert ui_check.compare({"a"}, {"z"}, exact=False)["unexpected"] == ["z"]


def test_exit_code_mapping():
    assert ui_check.exit_code([]) == 0
    assert ui_check.exit_code(["boom"]) == 1

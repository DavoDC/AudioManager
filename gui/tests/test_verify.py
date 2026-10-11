"""verify.run_steps: one PASS/FAIL line from named step results."""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from gui import verify


def test_all_steps_pass():
    code, line = verify.run_steps([("a", lambda: 0), ("b", lambda: 0)])
    assert code == 0 and line.startswith("[PASS]") and "a" in line and "b" in line


def test_failing_step_reported_and_later_steps_still_run():
    ran = []
    code, line = verify.run_steps([("a", lambda: 1), ("b", lambda: ran.append(1) or 0)])
    assert code == 1 and line.startswith("[FAIL]") and "a=1" in line and ran == [1]


def test_step_exception_counts_as_failure():
    def boom():
        raise RuntimeError("x")
    code, line = verify.run_steps([("a", boom)])
    assert code == 1 and "a=error" in line

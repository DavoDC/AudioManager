"""One-command GUI verification.

  python -m gui.verify            pytest + ui_check on a fixture server + quick capture matrix
  python -m gui.verify --full     the same with every fixture (several minutes)
  python -m gui.verify --fast     pytest + ui_check only

Prints one final line: [PASS] ... or [FAIL] name=code ... and exits 0 or 1.
scripts/dev/verify.bat runs the C# half and then this.
"""
from __future__ import annotations

import argparse
import subprocess
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

REPO = Path(__file__).resolve().parent.parent


def run_steps(steps) -> tuple[int, str]:
    """steps: [(name, callable returning an exit code)]. Every step runs."""
    results = []
    for name, fn in steps:
        started = time.monotonic()
        try:
            code = fn()
        except Exception as e:  # a crashing step is a failed step
            print(f"[step {name}] crashed: {e}")
            code = "error"
        results.append((name, code, time.monotonic() - started))
    bad = [(n, c) for n, c, _t in results if c != 0]
    if bad:
        return 1, "[FAIL] " + " ".join(f"{n}={c}" for n, c in bad)
    return 0, "[PASS] " + " + ".join(f"{n} ({t:.0f}s)" for n, _c, t in results)


def _pytest() -> int:
    return subprocess.call([sys.executable, "-m", "pytest", "gui/tests", "-q"], cwd=str(REPO))  # window-ok


def _ui_check() -> int:
    from gui import capture, ui_check
    server = capture.DemoServer("normal")
    try:
        return ui_check.main(["--url", server.base])
    finally:
        server.stop()


def _capture(scenarios: str) -> int:
    from gui import capture
    return capture.main(["--scenarios", scenarios])


def main(argv=None) -> int:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--full", action="store_true")
    p.add_argument("--fast", action="store_true")
    ns = p.parse_args(argv)
    steps = [("pytest", _pytest), ("ui_check", _ui_check)]
    if not ns.fast:
        steps.append(("capture", lambda: _capture(
            "normal,empty,error,long-names,huge,review,running" if ns.full else "normal,empty,error")))
    code, line = run_steps(steps)
    print(line)
    return code


if __name__ == "__main__":
    sys.exit(main())

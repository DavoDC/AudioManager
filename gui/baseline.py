"""Fixture baselines with a tolerance diff (OPS-4, WS-075).

  python -m gui.baseline check  [--fixture NAME ...] [--dir DIR]   exit 0 same, 1 changed (names the region)
  python -m gui.baseline update [--fixture NAME ...] [--dir DIR]   rewrite the committed baselines

A baseline is the fixture server's /state.json with the clock frozen out
(volatile keys dropped) and long row lists cut to the first MAX_ROWS rows, so
each file stays small and diffs read as text. A region is `tab > row testid >
field`. Numbers may drift by REL_TOL before they count as a change.
"""
from __future__ import annotations

import argparse
import json
import sys
import urllib.request
from pathlib import Path

BASELINES = Path(__file__).resolve().parent / "tests" / "baselines"
VOLATILE = {"generated_at", "boot_id", "built_at", "now", "timestamp", "build", "started_at", "repo"}
MAX_ROWS = 5
REL_TOL = 0.01


def normalize(doc):
    if isinstance(doc, dict):
        out = {}
        for k in sorted(doc):
            if k in VOLATILE:
                continue
            v = normalize(doc[k])
            if k == "rows" and isinstance(v, list):
                v = v[:MAX_ROWS]
            out[k] = v
        return out
    if isinstance(doc, list):
        return [normalize(x) for x in doc]
    return doc


def _close(a, b) -> bool:
    if isinstance(a, bool) or isinstance(b, bool):
        return a == b
    if isinstance(a, (int, float)) and isinstance(b, (int, float)):
        return abs(a - b) <= REL_TOL * max(abs(a), abs(b), 1e-9)
    return a == b


def _row_key(row, i):
    return row.get("testid", f"#{i}") if isinstance(row, dict) else f"#{i}"


def diff(old, new, path: str = "") -> list[str]:
    out: list[str] = []
    if isinstance(old, dict) and isinstance(new, dict):
        for k in sorted(set(old) | set(new)):
            here = f"{path} > {k}" if path else k
            if k not in new:
                out.append(f"{here}: removed (was {old[k]!r})"[:200])
            elif k not in old:
                out.append(f"{here}: added ({new[k]!r})"[:200])
            elif k == "rows" and isinstance(old[k], list) and isinstance(new[k], list):
                om = {_row_key(r, i): r for i, r in enumerate(old[k])}
                nm = {_row_key(r, i): r for i, r in enumerate(new[k])}
                for rk in om.keys() - nm.keys():
                    out.append(f"{here} > {rk}: row removed")
                for rk in nm.keys() - om.keys():
                    out.append(f"{here} > {rk}: row added")
                for rk in sorted(om.keys() & nm.keys()):
                    out.extend(diff(om[rk], nm[rk], f"{here} > {rk}"))
            else:
                out.extend(diff(old[k], new[k], here))
        return out
    if isinstance(old, list) and isinstance(new, list):
        if len(old) != len(new):
            out.append(f"{path}: length {len(old)} -> {len(new)}")
        for i, (a, b) in enumerate(zip(old, new)):
            out.extend(diff(a, b, f"{path}[{i}]"))
        return out
    if not _close(old, new):
        out.append(f"{path}: {old!r} -> {new!r}"[:200])
    return out


def snapshot(fixture: str) -> dict:
    from gui import capture
    server = capture.DemoServer(fixture)
    try:
        with urllib.request.urlopen(server.base + "/state.json", timeout=30) as r:
            return normalize(json.loads(r.read().decode("utf-8")))
    finally:
        server.stop()


def main(argv=None) -> int:
    p = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    p.add_argument("action", choices=("check", "update"))
    p.add_argument("--fixture", action="append")
    p.add_argument("--dir", default=str(BASELINES))
    ns = p.parse_args(argv)
    from gui import demo
    names = ns.fixture or list(demo.SCENARIOS)
    base = Path(ns.dir)
    bad = 0
    for name in names:
        new = snapshot(name)
        path = base / f"{name}.json"
        if ns.action == "update":
            base.mkdir(parents=True, exist_ok=True)
            path.write_text(json.dumps(new, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
            print(f"baseline updated: {name}")
            continue
        if not path.exists():
            print(f"FAIL {name}: no baseline at {path} (run `python -m gui.baseline update`)")
            bad += 1
            continue
        changes = diff(json.loads(path.read_text(encoding="utf-8")), new)
        for c in changes:
            print(f"FAIL {name} > {c}")
        bad += 1 if changes else 0
    if ns.action == "check":
        print(f"baseline: {'FAIL' if bad else 'PASS'} ({bad} fixture(s) changed)")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())

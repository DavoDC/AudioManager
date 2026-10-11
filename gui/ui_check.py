"""ui_check: HTTP-level harness against a running GUI (no browser needed).

  python -m gui.ui_check [--url http://localhost:8471] [--tab TAB]

For every tab it fetches /?tab=X and /state/X.json, then checks that the
data-testid values painted in the page match the state rows (exact for
server-rendered rows; painted subset of state for paginated Acquire).
It also checks the build footer, nav ids, security headers, and that a bad
Host header is refused. Exit 0 = all good, 1 = failures, 2 = cannot reach.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
import urllib.error
import urllib.request

# Tabs whose painted rows are a page of the state rows, not all of them.
PAGINATED = {"acquire"}
ROW_PREFIX = {"acquire": "row-acq", "integration": "row-int", "library": "row-lib",
              "stats": "stat-", "tagfix": "row-rule", "mirror": "row-mir", "services": "row-svc"}
_ATTR = re.compile(r'data-testid=\\?"([^"\\]+)\\?"')
_JSON = re.compile(r'"(?:testid|data-testid)"\s*:\s*"([^"]+)"')


def extract_testids(source: str, prefix: str = "") -> set[str]:
    found = set(_ATTR.findall(source)) | set(_JSON.findall(source))
    return {t for t in found if t.startswith(prefix)}


def compare(state_ids: set[str], painted: set[str], exact: bool) -> dict:
    missing = sorted(state_ids - painted) if exact else []
    return {"missing": missing, "unexpected": sorted(painted - state_ids)}


def exit_code(failures: list[str]) -> int:
    return 1 if failures else 0


def _get(url: str, headers: dict | None = None) -> tuple[int, dict, str]:
    req = urllib.request.Request(url, headers=headers or {})
    try:
        with urllib.request.urlopen(req, timeout=15) as r:
            return r.status, dict(r.headers), r.read().decode("utf-8", "replace")
    except urllib.error.HTTPError as e:
        return e.code, dict(e.headers), e.read().decode("utf-8", "replace")


def favicon_problems(page: str) -> list[str]:
    """The page must link the state favicon (WS-124)."""
    return [] if 'data-testid="favicon"' in page and "/api/favicon.png" in page else ["page lacks the state favicon link"]


def check(base: str, only_tab: str | None = None) -> list[str]:
    from gui.uistate import TAB_LABELS
    fails: list[str] = []
    code, headers, body = _get(f"{base}/api/build")
    if code != 200:
        return [f"/api/build returned {code}"]
    if "nosniff" not in {k.lower(): v for k, v in headers.items()}.get("x-content-type-options", "").lower():
        fails.append("missing X-Content-Type-Options: nosniff")
    code, _h, _b = _get(f"{base}/", {"Host": "evil.example"})
    if code != 403:
        fails.append(f"bad Host header not refused (got {code})")
    for tab, _label in TAB_LABELS:
        if only_tab and tab != only_tab:
            continue
        code, _h, state_body = _get(f"{base}/state/{tab}.json")
        if code != 200:
            fails.append(f"{tab}: /state/{tab}.json returned {code}")
            continue
        state = json.loads(state_body)
        if state.get("error"):
            fails.append(f"{tab}: state error {state['error']}")
        code, _h, page = _get(f"{base}/?tab={tab}")
        if code != 200:
            fails.append(f"{tab}: page returned {code}")
            continue
        fails.extend(f"{tab}: {m}" for m in favicon_problems(page))
        for needle in ("build-footer", f"nav-{tab}", f"tab-{tab}"):
            if needle not in page:
                fails.append(f"{tab}: page lacks data-testid {needle}")
        state_ids = {r["testid"] for r in state["rows"]}
        if not state_ids:
            continue
        painted = extract_testids(page, ROW_PREFIX[tab])
        res = compare(state_ids, painted, exact=tab not in PAGINATED)
        for t in res["missing"]:
            fails.append(f"{tab}: state row not painted: {t}")
        for t in res["unexpected"]:
            fails.append(f"{tab}: painted id not in state: {t}")
    return fails


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    p.add_argument("--url", default="http://localhost:8471")
    p.add_argument("--tab")
    ns = p.parse_args(argv)
    try:
        fails = check(ns.url.rstrip("/"), ns.tab)
    except (urllib.error.URLError, ConnectionError, OSError) as e:
        print(f"ui_check: cannot reach {ns.url}: {e}", file=sys.stderr)
        return 2
    for f in fails:
        print(f"FAIL {f}")
    print(f"ui_check: {'FAIL' if fails else 'PASS'} ({len(fails)} problem(s))")
    return exit_code(fails)


if __name__ == "__main__":
    sys.exit(main())

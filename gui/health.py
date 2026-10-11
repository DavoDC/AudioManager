"""The one health verdict (WS-052): green, amber or red plus the reasons.

Header word, tray, favicon and GET /api/health all read this module, so they cannot
disagree. verdict() is pure (testable); compute() gathers live inputs and never raises.
"""
from __future__ import annotations

import time

WORDS = {"green": "Healthy", "amber": "Attention", "red": "Broken"}
ERROR_WINDOW_S = 600  # browser/tab errors in the last ten minutes count


def word(verdict: str) -> str:
    return WORDS.get(verdict, "Unknown")


def verdict(*, exe_ok: bool, data_ok: bool, data_error: str | None,
            recent_errors: int, fixture: str | None) -> dict:
    reasons: list[str] = []
    level = "green"
    if fixture:
        level = "amber"
        reasons.append(f"Fixture mode ({fixture}): demo data, exe disabled")
    elif not exe_ok:
        level = "red"
        reasons.append("AudioManager.exe not found: build it with scripts/dev/build.bat")
    if not data_ok:
        if level == "green":
            level = "amber"
        reasons.append(data_error or "analysis data not loaded")
    if recent_errors:
        if level == "green":
            level = "amber"
        reasons.append(f"{recent_errors} app error(s) logged in the last 10 minutes")
    return {"verdict": level, "word": word(level), "reasons": reasons}


def _recent_error_count() -> int:
    from gui import applog
    cutoff = time.strftime("%H:%M:%S", time.localtime(time.time() - ERROR_WINDOW_S))
    now = time.strftime("%H:%M:%S")
    if cutoff > now:  # window crossed midnight: today's log only, count all
        cutoff = "00:00:00"
    n = 0
    for line in applog.tail(200):
        if "[app-error]" in line or "[tab-build-failed]" in line:
            if line[:8] >= cutoff:
                n += 1
    return n


def compute() -> dict:
    try:
        from gui import config, demo
        from gui.state import state
        return verdict(exe_ok=config.EXE_PATH.exists(),
                       data_ok=state.load_error is None and state.stats is not None,
                       data_error=state.load_error, recent_errors=_recent_error_count(),
                       fixture=demo.fixture_name())
    except Exception as e:  # health must answer even when the app is sick
        return {"verdict": "red", "word": word("red"), "reasons": [f"health check failed: {e}"]}

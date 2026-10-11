"""HTTP surface for state, build info, agent manifest and the browser log relay.

All routes are GET except POST /__log (token protected). Read-only: nothing
here mutates the library or runs the exe.
"""
from __future__ import annotations

from fastapi import Request
from fastapi.responses import JSONResponse

from gui import applog, buildinfo, demo, health, security, uistate

NO_STORE = {"Cache-Control": "no-store"}

ENDPOINTS = [
    {"path": "/api/build", "method": "GET", "returns": "build line, sha, boot_id, fixture"},
    {"path": "/api/health", "method": "GET", "returns": "verdict green/amber/red, word, reasons"},
    {"path": "/state.json", "method": "GET", "returns": "every tab's state contract"},
    {"path": "/state/{tab}.json", "method": "GET", "returns": "one tab's state contract"},
    {"path": "/api/manifest.json", "method": "GET", "returns": "this agent manifest"},
    {"path": "/__log", "method": "POST", "returns": "relay a browser error line (X-AM-Token)"},
]


def manifest() -> dict:
    from gui import actions
    return {
        "name": "AudioManager GUI", "schema": uistate.SCHEMA,
        "build": buildinfo.api_payload(demo.fixture_name()),
        "tabs": [{"key": k, "label": label, "url": f"/?tab={k}", "state": f"/state/{k}.json"}
                 for k, label in uistate.TAB_LABELS],
        "endpoints": ENDPOINTS,
        "actions": actions.describe(),
        "debug": "window.__debug: state(), tabState(tab), setTab(key), forceStatus(text, kind), help()",
        "docs": ["docs/References/GUI-State-Contract.md", "docs/DESIGN.md"],
        "log": str(applog.path_today()),
    }


def register(app) -> None:
    def j(payload, status=200):
        return JSONResponse(payload, status_code=status, headers=NO_STORE)

    async def api_build():
        return j(buildinfo.api_payload(demo.fixture_name()))

    async def api_health():
        return j(health.compute())

    async def state_all():
        return j(uistate.full_state())

    async def state_tab(tab: str):
        p = uistate.tab_state(tab)
        return j(p if p is not None else {"error": f"unknown tab {tab}"}, 200 if p else 404)

    async def api_manifest():
        return j(manifest())

    async def log_relay(request: Request):
        if not security.check_token(request.headers.get("x-am-token", "")):
            return j({"error": "bad token"}, 403)
        try:
            body = await request.json()
        except Exception:
            return j({"error": "bad json"}, 400)
        applog.write("app-error", f"{body.get('kind', '?')}: {body.get('message', '')}")
        return j({"ok": True})

    app.add_api_route("/api/build", api_build, methods=["GET"])
    app.add_api_route("/api/health", api_health, methods=["GET"])
    app.add_api_route("/state.json", state_all, methods=["GET"])
    app.add_api_route("/state/{tab}.json", state_tab, methods=["GET"])
    app.add_api_route("/api/manifest.json", api_manifest, methods=["GET"])
    app.add_api_route("/__log", log_relay, methods=["POST"])

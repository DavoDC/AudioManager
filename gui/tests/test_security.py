"""Loopback guard (WS-020/021/022/032): bind constant, Host allowlist, Origin check, headers."""
import asyncio
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from gui import security


def test_bind_host_is_the_literal_loopback_ip():
    assert security.BIND_HOST == "127.0.0.1"
    assert security.open_url(8471) == "http://localhost:8471/"


def test_allowed_hosts_need_the_port():
    hosts = security.allowed_hosts(8471)
    assert "127.0.0.1:8471" in hosts and "localhost:8471" in hosts
    assert "127.0.0.1" not in hosts and "evil.example:8471" not in hosts


def _call(scope_extra, port=8471):
    """Drive the ASGI guard once; return (status or 'passed', headers dict)."""
    sent = []

    async def inner(scope, receive, send):
        await send({"type": "http.response.start", "status": 200, "headers": []})
        await send({"type": "http.response.body", "body": b"ok"})

    async def send(msg):
        sent.append(msg)

    async def receive():
        return {"type": "http.request", "body": b""}

    guard = security.LoopbackGuard(inner, port_fn=lambda: port)
    scope = {"type": "http", "method": "GET", "path": "/", "headers": []}
    scope.update(scope_extra)
    asyncio.run(guard(scope, receive, send))
    start = next(m for m in sent if m["type"] == "http.response.start")
    return start["status"], {k.decode(): v.decode() for k, v in start["headers"]}


def hdr(host=None, origin=None):
    out = []
    if host is not None:
        out.append((b"host", host.encode()))
    if origin is not None:
        out.append((b"origin", origin.encode()))
    return out


def test_good_host_passes_and_gets_security_headers():
    status, headers = _call({"headers": hdr("127.0.0.1:8471")})
    assert status == 200
    assert headers["x-content-type-options"] == "nosniff"
    assert "frame-ancestors 'none'" in headers["content-security-policy"]


def test_rebinding_host_is_refused():
    status, _ = _call({"headers": hdr("evil.example:8471")})
    assert status == 403


def test_missing_host_is_refused():
    status, _ = _call({"headers": []})
    assert status == 403


def test_cross_origin_post_is_refused_same_origin_passes():
    assert _call({"method": "POST", "headers": hdr("localhost:8471", "http://evil.example")})[0] == 403
    assert _call({"method": "POST", "headers": hdr("localhost:8471", "http://localhost:8471")})[0] == 200


def test_json_paths_are_no_store():
    _, headers = _call({"path": "/state.json", "headers": hdr("localhost:8471")})
    assert headers["cache-control"] == "no-store"


def test_websocket_with_foreign_origin_is_closed():
    sent = []

    async def inner(scope, receive, send):
        raise AssertionError("must not reach the app")

    async def send(msg):
        sent.append(msg)

    async def receive():
        return {"type": "websocket.connect"}

    guard = security.LoopbackGuard(inner, port_fn=lambda: 8471)
    scope = {"type": "websocket", "path": "/_nicegui_ws", "headers": hdr("localhost:8471", "http://evil.example")}
    asyncio.run(guard(scope, receive, send))
    assert sent and sent[0]["type"] == "websocket.close"


def test_token_check():
    assert security.check_token(security.TOKEN)
    assert not security.check_token("nope") and not security.check_token(None)

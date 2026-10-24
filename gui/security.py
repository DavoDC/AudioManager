"""Loopback security for the GUI (webapp standard WS-020, 021, 022, 031, 032).

- BIND_HOST is the literal 127.0.0.1 (a hostname can resolve to IPv6 or something else);
  the URL we open and print is http://localhost:PORT, a separate value (decision D5).
- LoopbackGuard is a pure ASGI middleware, so it covers HTTP and the NiceGUI websocket:
  Host must be localhost or 127.0.0.1 with this server's port (stops DNS rebinding);
  a non-GET request or a websocket upgrade that carries an Origin must name the same
  loopback origin (stops cross-site posts). curl sends no Origin, so scripts still work.
- TOKEN is per launch, embedded in the served page; every non-GET HTTP request requires it in
  X-AM-Token (NiceGUI's own transport under /_nicegui is exempt: it carries its own client id).
- Responses get nosniff, a frame-ancestors lock, and no-store on JSON and debug paths.
"""
from __future__ import annotations

import hmac
import secrets

BIND_HOST = "127.0.0.1"
DEFAULT_PORT = 8471
TOKEN = secrets.token_urlsafe(24)

_SAFE_METHODS = {"GET", "HEAD", "OPTIONS"}
_TOKEN_EXEMPT_PREFIXES = ("/_nicegui",)
_NO_STORE_PREFIXES = ("/state", "/api/", "/__")
_SECURITY_HEADERS = [
    (b"x-content-type-options", b"nosniff"),
    (b"x-frame-options", b"DENY"),
    (b"content-security-policy", b"frame-ancestors 'none'"),
]


def open_url(port: int, path: str = "/") -> str:
    return f"http://localhost:{port}{path}"


def allowed_hosts(port: int) -> set[str]:
    return {f"127.0.0.1:{port}", f"localhost:{port}"}


def allowed_origins(port: int) -> set[str]:
    return {f"http://127.0.0.1:{port}", f"http://localhost:{port}"}


def check_token(value) -> bool:
    return isinstance(value, str) and hmac.compare_digest(value, TOKEN)


def _header(scope, name: bytes) -> str | None:
    for key, value in scope.get("headers", []):
        if key.lower() == name:
            return value.decode("latin-1")
    return None


class LoopbackGuard:
    def __init__(self, app, port_fn):
        self.app = app
        self.port_fn = port_fn

    async def __call__(self, scope, receive, send):
        kind = scope.get("type")
        if kind not in ("http", "websocket"):
            return await self.app(scope, receive, send)
        port = self.port_fn()
        host = _header(scope, b"host")
        origin = _header(scope, b"origin")
        refuse = None
        if host is None or host.lower() not in allowed_hosts(port):
            refuse = "bad Host"
        elif origin is not None and (kind == "websocket" or scope.get("method", "GET") not in _SAFE_METHODS):
            if origin.lower() not in allowed_origins(port):
                refuse = "bad Origin"
        if refuse is None and kind == "http" and scope.get("method", "GET") not in _SAFE_METHODS \
                and not scope.get("path", "").startswith(_TOKEN_EXEMPT_PREFIXES) \
                and not check_token(_header(scope, b"x-am-token") or ""):
            refuse = "missing or wrong token"
        if refuse:
            if kind == "websocket":
                return await send({"type": "websocket.close", "code": 1008})
            body = f"403 {refuse}".encode()
            await send({"type": "http.response.start", "status": 403,
                        "headers": [(b"content-type", b"text/plain"), (b"content-length", str(len(body)).encode())]
                        + _SECURITY_HEADERS})
            return await send({"type": "http.response.body", "body": body})
        if kind == "websocket":
            return await self.app(scope, receive, send)

        path = scope.get("path", "")
        no_store = path.endswith(".json") or path.startswith(_NO_STORE_PREFIXES)

        async def send_wrapped(message):
            if message["type"] == "http.response.start":
                headers = [(k, v) for k, v in message.get("headers", [])]
                have = {k.lower() for k, _ in headers}
                headers += [(k, v) for k, v in _SECURITY_HEADERS if k not in have]
                if no_store and b"cache-control" not in have:
                    headers.append((b"cache-control", b"no-store"))
                message = {**message, "headers": headers}
            await send(message)

        await self.app(scope, receive, send_wrapped)

"""A refused POST whose body arrives late must still get its 403, never a connection reset.

Guards the sibling of the unread-POST-body flake: LoopbackGuard answers a refused write
before the app reads the body. Real uvicorn on a spare loopback port, raw sockets, headers
first and the body 0.3 s later, 20 clients at once, 3 batches. The refusal is a foreign
Origin (a cross-site write), which LoopbackGuard checks before it looks at the body.
Offline: bare FastAPI app, no NiceGUI, no audio devices, no running service."""
from __future__ import annotations

import socket
import threading
import time
from concurrent.futures import ThreadPoolExecutor

from fastapi import FastAPI

from gui import security

CLIENTS = 20
BATCHES = 3
BODY = b"x" * 65536
EXPECTED = "HTTP/1.1 403 Forbidden"


def _free_port() -> int:
    with socket.socket() as s:
        s.bind(("127.0.0.1", 0))
        return s.getsockname()[1]


def _start(port):
    import uvicorn

    app = FastAPI()
    app.add_middleware(security.LoopbackGuard, port_fn=lambda: port)

    @app.post("/x")
    def px():
        return {"ok": 1}

    server = uvicorn.Server(uvicorn.Config(app, host="127.0.0.1", port=port, log_level="error"))
    thread = threading.Thread(target=server.run, daemon=True)
    thread.start()
    for _ in range(100):
        if server.started:
            return server, thread
        time.sleep(0.05)
    raise RuntimeError("uvicorn did not start")


def _one(port):
    """Refused POST (foreign Origin): headers, wait, body. Returns the status line or the error name."""
    try:
        with socket.create_connection(("127.0.0.1", port), timeout=10) as s:
            head = (f"POST /x HTTP/1.1\r\nHost: localhost:{port}\r\nOrigin: http://evil.example\r\n"
                    f"Content-Length: {len(BODY)}\r\nContent-Type: text/plain\r\n\r\n").encode()
            s.sendall(head)
            time.sleep(0.3)
            try:
                s.sendall(BODY)
            except OSError:
                pass  # server already answered and closed; the response must still be readable below
            data = b""
            while b"\r\n\r\n" not in data:
                chunk = s.recv(4096)
                if not chunk:
                    break
                data += chunk
            return data.split(b"\r\n", 1)[0].decode("latin-1")
    except OSError as exc:
        return type(exc).__name__


def test_refused_post_with_late_body_gets_403_not_reset():
    port = _free_port()
    server, thread = _start(port)
    try:
        results = []
        for _ in range(BATCHES):
            with ThreadPoolExecutor(CLIENTS) as pool:
                results += list(pool.map(lambda _i: _one(port), range(CLIENTS)))
        bad = [r for r in results if r != EXPECTED]
        assert not bad, f"{len(bad)} of {len(results)} not a clean 403: {sorted(set(bad))}"
    finally:
        server.should_exit = True
        thread.join(timeout=10)

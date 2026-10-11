"""LCH-4: the port bind is retried briefly so a lingering socket after os.execv
does not kill the GUI; a permanently held port fails with a clear message."""
import socket
import sys
import threading
import time
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from gui import bindwait


def _hold():
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    s.bind(("127.0.0.1", 0))
    s.listen(1)
    return s, s.getsockname()[1]


def test_free_port_returns_immediately():
    s, port = _hold()
    s.close()
    assert bindwait.wait_for_port("127.0.0.1", port, timeout=1.0) < 0.5


def test_port_released_after_a_delay_succeeds():
    s, port = _hold()
    threading.Timer(0.4, s.close).start()
    waited = bindwait.wait_for_port("127.0.0.1", port, timeout=5.0, interval=0.05)
    assert 0.2 <= waited < 4.0


def test_permanently_held_port_fails_with_clear_message():
    s, port = _hold()
    try:
        t0 = time.monotonic()
        with pytest.raises(bindwait.PortInUse) as e:
            bindwait.wait_for_port("127.0.0.1", port, timeout=0.3, interval=0.05)
        assert time.monotonic() - t0 < 3.0
        assert str(port) in str(e.value) and "--stop" in str(e.value)
    finally:
        s.close()

"""Wait for the listen port to be free before the server binds (LCH-4, WS-006).

After os.execv (hot reload) the old socket can linger for a moment; binding at
once then fails and the GUI dies silently under pythonw. `wait_for_port` retries
a throwaway bind for a short time and raises PortInUse with a plain message if
the port stays held.
"""
from __future__ import annotations

import socket
import time


class PortInUse(RuntimeError):
    """The port stayed held for the whole retry window."""


def _can_bind(host: str, port: int) -> bool:
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    try:
        s.bind((host, port))
        return True
    except OSError:
        return False
    finally:
        s.close()


def wait_for_port(host: str, port: int, timeout: float = 5.0, interval: float = 0.2,
                  sleep=time.sleep, clock=time.monotonic) -> float:
    """Return seconds waited once `port` is bindable; raise PortInUse after `timeout`."""
    start = clock()
    while True:
        if _can_bind(host, port):
            return clock() - start
        if clock() - start >= timeout:
            raise PortInUse(
                f"port {port} on {host} is still in use after {timeout:g}s - another "
                f"AudioManager GUI is probably running; run `python -m gui.main --stop` "
                f"or pass --port N")
        sleep(interval)

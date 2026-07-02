"""Serialized subprocess runner for AudioManager.exe.

Mandatory discipline (from the brief), enforced here in one place:
- `--no-input` is ALWAYS appended (a subprocess with no stdin hangs forever
  on the exe's interactive confirm path).
- stdout AND stderr are captured, merged, and read line-by-line unbuffered.
- One exe invocation at a time - a module-level lock; callers disable their
  trigger buttons while `runner.busy` is True.
- Every call has a timeout and can be cancelled (kills the process tree).

Exit codes (from Program.cs): 0 success; 1 gate/validation failure or
unknown mode; 123 unhandled exception (Message: + Stack Trace: block).
"""
from __future__ import annotations

import asyncio
import subprocess
from dataclasses import dataclass, field
from typing import Callable

from gui import config


@dataclass
class RunResult:
    command: list[str]
    returncode: int | None = None       # None => cancelled or timed out
    lines: list[str] = field(default_factory=list)
    cancelled: bool = False
    timed_out: bool = False
    failed_to_start: str | None = None  # OSError message if spawn failed

    @property
    def ok(self) -> bool:
        return self.returncode == 0

    @property
    def output(self) -> str:
        return "\n".join(self.lines)

    @property
    def command_line(self) -> str:
        return subprocess.list2cmdline(self.command)

    def first_error_line(self) -> str | None:
        """The exe's first '- ERROR:' / 'ERROR' line - the actionable cause
        for exit-1 gate failures."""
        for ln in self.lines:
            stripped = ln.strip()
            if stripped.upper().startswith(("- ERROR", "ERROR", "[ERROR]")):
                return stripped.lstrip("- ").strip()
        return None

    def stack_trace_block(self) -> str | None:
        """For exit 123: the exe prints 'Message:' and 'Stack Trace:' blocks."""
        for i, ln in enumerate(self.lines):
            if ln.strip().startswith(("Message:", "Stack Trace:")):
                return "\n".join(self.lines[i:])
        return None

    def interpreted(self, action: str) -> str:
        """Human meaning of the outcome, for the error modal's headline slot."""
        if self.failed_to_start:
            return f"Could not start AudioManager.exe: {self.failed_to_start}"
        if self.cancelled:
            return f"{action} was cancelled."
        if self.timed_out:
            return f"{action} timed out and was killed."
        if self.returncode == 0:
            return f"{action} completed."
        if self.returncode == 1:
            cause = self.first_error_line()
            return f"Exit code 1 - {cause}" if cause else (
                "Exit code 1 - validation/gate failure (see details)")
        if self.returncode == 123:
            return "Exit code 123 - the exe hit an unhandled exception (see details)"
        return f"Exit code {self.returncode} - unexpected failure (see details)"


class ExeRunner:
    """One exe invocation at a time, streamed line-by-line."""

    def __init__(self):
        self._lock = asyncio.Lock()
        self._proc: asyncio.subprocess.Process | None = None
        self._cancel_requested = False
        self.busy: bool = False
        self.current_action: str = ""

    async def run(
        self,
        args: list[str],
        action: str = "Operation",
        on_line: Callable[[str], None] | None = None,
        timeout: float = config.TIMEOUT_ANALYSIS,
    ) -> RunResult:
        """Run AudioManager.exe with args (--no-input auto-appended)."""
        cmd = [str(config.EXE_PATH), *args]
        if "--no-input" not in cmd:
            cmd.append("--no-input")
        result = RunResult(command=cmd)

        async with self._lock:
            self.busy = True
            self._cancel_requested = False
            self.current_action = action
            try:
                try:
                    self._proc = await asyncio.create_subprocess_exec(
                        *cmd,
                        stdout=asyncio.subprocess.PIPE,
                        stderr=asyncio.subprocess.STDOUT,
                        stdin=asyncio.subprocess.DEVNULL,
                        cwd=str(config.REPO_ROOT),
                    )
                except OSError as e:
                    result.failed_to_start = str(e)
                    return result

                async def _read():
                    assert self._proc and self._proc.stdout
                    while True:
                        raw = await self._proc.stdout.readline()
                        if not raw:
                            break
                        line = raw.decode("utf-8", errors="replace").rstrip("\r\n")
                        result.lines.append(line)
                        if on_line:
                            try:
                                on_line(line)
                            except Exception:
                                pass  # a UI callback error must not kill the read loop

                try:
                    await asyncio.wait_for(_read(), timeout=timeout)
                    result.returncode = await self._proc.wait()
                    if self._cancel_requested:
                        result.cancelled = True
                        result.returncode = None
                except asyncio.TimeoutError:
                    result.timed_out = True
                    self._kill_tree()
                except asyncio.CancelledError:
                    result.cancelled = True
                    self._kill_tree()
                    raise
                return result
            finally:
                self._proc = None
                self.busy = False
                self.current_action = ""

    def cancel(self) -> None:
        """Kill the running process tree (Cancel button)."""
        self._cancel_requested = True
        self._kill_tree()

    def _kill_tree(self) -> None:
        proc = self._proc
        if proc is None or proc.returncode is not None:
            return
        try:
            # taskkill /T kills the whole tree - the exe may spawn git.
            subprocess.run(
                ["taskkill", "/PID", str(proc.pid), "/T", "/F"],
                capture_output=True,
                timeout=15,
            )
        except (OSError, subprocess.TimeoutExpired):
            try:
                proc.kill()
            except ProcessLookupError:
                pass


runner = ExeRunner()

"""Subprocess Error Modal - built once, reused for EVERY exe call.

Layout per the brief: title '<action> failed'; interpreted meaning (exit
code -> human cause, first ERROR line for gates, Stack Trace block for 123);
collapsible monospace details; Copy (full command + exit code + output) and
Retry (re-runs the identical command).
"""
from __future__ import annotations

from typing import Awaitable, Callable

from nicegui import ui

from gui.runner import RunResult


def show_error_modal(
    action: str,
    result: RunResult,
    retry: Callable[[], Awaitable[None]] | None = None,
) -> None:
    with ui.dialog() as dialog, ui.card().classes("err-modal").style("padding:18px 20px;gap:12px;"):
        with ui.row().classes("w-full items-center justify-between"):
            ui.label(f"{action} failed").classes("err-title")
            ui.button(icon="close", on_click=dialog.close).props("flat round dense size=sm color=grey")

        ui.label(result.interpreted(action)).classes("err-meaning")

        detail_lines = result.lines[-200:]
        if result.returncode == 123 and result.stack_trace_block():
            detail_lines = result.stack_trace_block().splitlines()
        detail_text = (
            f"> {result.command_line}\n"
            + "\n".join(f"> {ln}" for ln in detail_lines)
        )
        copy_payload = (
            f"Command: {result.command_line}\n"
            f"Exit code: {result.returncode}\n"
            f"--- output ---\n{result.output}"
        )

        with ui.expansion("Details", icon="terminal").classes("w-full") \
                .style("border:1px solid var(--panel-border);border-radius:3px;"):
            with ui.row().classes("w-full justify-end").style("padding:4px 8px 0;"):
                def do_copy():
                    ui.clipboard.write(copy_payload)
                    ui.notify("Copied command + output to clipboard", type="positive")
                ui.button("Copy", icon="content_copy", on_click=do_copy) \
                    .props("flat dense size=sm color=grey")
            ui.html(f'<div class="console err-details">{_escape(detail_text)}</div>')

        with ui.row().classes("w-full justify-end").style("gap:10px;"):
            ui.button("Dismiss", on_click=dialog.close).props("flat color=grey")
            if retry is not None:
                async def do_retry():
                    dialog.close()
                    await retry()
                ui.button("Retry execution", on_click=do_retry).props("unelevated color=primary")

    dialog.open()
    dialog.on("hide", lambda: dialog.clear())


def _escape(text: str) -> str:
    return text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")

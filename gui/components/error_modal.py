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


def show_cancelled_modal(on_run_analysis: Callable[[], None]) -> None:
    """A real integration run cancelled mid-batch is not a subprocess failure
    (result.interpreted() already distinguishes cancelled from failed) - it is
    a partial-mutation warning. `runner.cancel()` does a `taskkill /T /F`
    partway through a sequence of real file moves, so some files may already
    be in the library and the rest still in NewMusic, and the post-run mirror
    update + LibChecker safety check never ran. Deliberately no "Retry
    execution" button here: retrying blindly against files that may have
    already moved is not safe to offer without knowing which ones moved."""
    with ui.dialog() as dialog, ui.card().classes("err-modal").style("padding:18px 20px;gap:12px;"):
        with ui.row().classes("w-full items-center justify-between"):
            ui.label("Integration cancelled partway through").classes("err-title")
            ui.button(icon="close", on_click=dialog.close).props("flat round dense size=sm color=grey")

        ui.label(
            "Some files may already be in the library and the rest may still be in NewMusic. "
            "The post-run mirror update and LibChecker safety check did not run, so the "
            "library's state has not been verified. Run Analysis before the next integration "
            "batch to see exactly where things stand."
        ).classes("err-meaning")

        with ui.row().classes("w-full justify-end").style("gap:10px;"):
            ui.button("Dismiss", on_click=dialog.close).props("flat color=grey")

            def do_run_analysis():
                dialog.close()
                on_run_analysis()
            ui.button("Run Analysis Now", on_click=do_run_analysis).props("unelevated color=primary")

    dialog.open()
    dialog.on("hide", lambda: dialog.clear())


def _escape(text: str) -> str:
    return text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")

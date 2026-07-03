"""Tag Fix tab (skeleton - honest about what it is).

The rule cards below DESCRIBE the exe's real TagFixer transforms, which are
fixed and hardcoded (TCMP, genre normalisation, parentheticals, featured-
artist extraction) and apply only to NewMusic. The Maintainerr-style "rule
builder" is a future capability that needs a C# change - this tab does not
pretend otherwise. "Run Fixed Rules (dry run)" triggers the exe's existing
`tagfix --dry-run` and shows its output.
"""
from __future__ import annotations

import asyncio

from nicegui import ui

from gui import config
from gui.components.error_modal import show_error_modal
from gui.runner import runner

# The exe's actual hardcoded transforms, presented as read-only rule cards.
FIXED_RULES = [
    ("Compilation flag (TCMP)", "Sets the iTunes TCMP compilation frame where album context requires it",
     "Active", "NewMusic only", "hardcoded"),
    ("Genre normalisation", "Maps raw genre strings onto the library's canonical genre set",
     "Active", "NewMusic only", "hardcoded"),
    ("Strip parentheticals", "Removes '(Explicit)', '(Official Video)' and similar noise from titles",
     "Active", "NewMusic only", "hardcoded"),
    ("Extract featured artists", "Moves 'feat. X' out of the title into the Artists field",
     "Active", "NewMusic only", "hardcoded"),
    ("File renames", "Renames files to the library's 'Artist - Title.mp3' convention",
     "Active", "NewMusic only", "hardcoded"),
]


class TagFixState:
    def __init__(self):
        self.lines: list[str] = []
        self.refresh = lambda: None


T = TagFixState()


def build() -> None:
    with ui.element("header").classes("page"):
        ui.html("<h1>Tag Fix</h1>")
        ui.html('<div class="meta">Fixed tag cleanup for NewMusic - rule builder is future work</div>')

    with ui.element("div").classes("gap-note w-full").style("margin-bottom:16px;"):
        ui.html(
            "<b>What this tab really is:</b> the exe&#39;s TagFixer applies the FIXED transforms "
            "below to NewMusic - it is not yet rule-configurable. The cards document the real "
            "behavior; a configurable rule builder (new/edit/delete, custom conditions) needs a "
            "C# change and is tracked as future work in IDEAS.md. Nothing here edits "
            "library files - TagFixer only ever touches the NewMusic inbox."
        )

    @ui.refreshable
    def content():
        with ui.row().style("gap:10px;margin-bottom:16px;flex-wrap:wrap;align-items:center;"):
            ui.button("+ New Rule").props("unelevated color=primary disable") \
                .tooltip("Needs the configurable-rules C# change - future work")
            if runner.busy:
                ui.html('<div class="note" style="margin:0;"><span class="spin"></span>'
                        f"{runner.current_action} running&hellip;</div>")
                ui.button("Cancel", on_click=lambda: runner.cancel()).props("outline dense color=negative size=sm")
            else:
                ui.button("Run Fixed Rules (dry run)",
                          on_click=lambda: asyncio.create_task(run_tagfix())) \
                    .props("outline color=primary") \
                    .tooltip("Runs the exe's existing tagfix --dry-run on NewMusic - no files changed")

        for name, desc, status, scope, kind in FIXED_RULES:
            with ui.element("div").classes("rule-card"):
                ui.html(
                    '<div style="display:flex;justify-content:space-between;align-items:center;">'
                    f'<div class="rule-name">{name}</div>'
                    f'<span class="status-badge active">{status}</span></div>'
                    f'<div class="rule-desc">{desc}</div>'
                    '<div class="rule-meta">'
                    f"<div>Applies to<b>{scope}</b></div>"
                    f"<div>Type<b>{kind}</b></div>"
                    "<div>Editable<b>Not yet (C# change)</b></div>"
                    "</div>"
                )

        if T.lines:
            with ui.expansion("Dry-run output").classes("w-full") \
                    .style("margin-top:14px;border:1px solid var(--panel-border);border-radius:3px;"):
                tail = "\n".join(T.lines[-300:])
                ui.html(f'<div class="console" style="max-height:260px;">{_esc(tail)}</div>')

    T.refresh = content.refresh
    content()


async def run_tagfix() -> None:
    if runner.busy:
        ui.notify("Another operation is already running", type="warning")
        return
    T.lines = []
    T.refresh()
    result = await runner.run(
        ["tagfix", "--dry-run"],
        action="Tag fix (dry run)",
        on_line=T.lines.append,
        timeout=config.TIMEOUT_TAGFIX,
    )
    T.refresh()
    if not result.ok and not result.cancelled:
        show_error_modal("Tag fix (dry run)", result, retry=run_tagfix)
    elif result.ok:
        ui.notify("Dry run complete - output below", type="positive")


def _esc(text: str) -> str:
    return text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")

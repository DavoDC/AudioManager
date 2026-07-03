"""Mirror tab - AudioMirror sync status + one-click commit.

Reads status via git log/status. The ONLY write this tab performs is the
explicit, user-confirmed **Commit AudioMirror** action (git add -A + git
commit in the mirror repo) - the last step that used to require a terminal
in the integration loop. It never touches mirror XML content itself, never
pushes, and never commits without the confirm dialog.
"""
from __future__ import annotations

import asyncio
import subprocess

from nicegui import ui

from gui import config


def _git(*args: str, check: bool = True) -> str | None:
    try:
        out = subprocess.run(
            ["git", "-C", str(config.AUDIOMIRROR_REPO), *args],
            capture_output=True, text=True, encoding="utf-8", errors="replace",
            timeout=60,
        )
        if check and out.returncode != 0:
            return None
        return (out.stdout or "").strip()
    except (OSError, subprocess.TimeoutExpired):
        return None


def _git_result(*args: str) -> tuple[int, str]:
    """Returncode + combined output, for the commit action's error reporting."""
    try:
        out = subprocess.run(
            ["git", "-C", str(config.AUDIOMIRROR_REPO), *args],
            capture_output=True, text=True, encoding="utf-8", errors="replace",
            timeout=120,
        )
        return out.returncode, ((out.stdout or "") + (out.stderr or "")).strip()
    except (OSError, subprocess.TimeoutExpired) as e:
        return -1, str(e)


def build() -> None:
    with ui.element("header").classes("page"):
        ui.html("<h1>Mirror</h1>")
        ui.html('<div class="meta">AudioMirror sync status &middot; one-click commit when dirty</div>')

    @ui.refreshable
    def content():
        last = _git("log", "-1", "--pretty=format:%h - %ad - %s", "--date=format:%Y-%m-%d %H:%M")
        status = _git("status", "--porcelain")
        if last is None and status is None:
            with ui.element("div").classes("panel w-full"):
                ui.label("Could not read the AudioMirror repo "
                         f"({config.AUDIOMIRROR_REPO}).").classes("note").style("margin:0;")
            return

        dirty = [ln for ln in (status or "").splitlines() if ln.strip()]
        n_dirty = len(dirty)
        clean = n_dirty == 0

        with ui.element("div").classes("panel w-full").style("margin-bottom:16px;"):
            with ui.element("div").classes("panel-title"):
                ui.html("<span>Status</span>")
                ui.button(icon="refresh", on_click=content.refresh).props("flat round dense size=sm color=grey")
            dot = "fresh-dot" if clean else "fresh-dot stale"
            state_label = ("In sync - ready for integration" if clean
                           else f"{n_dirty} uncommitted change{'s' if n_dirty != 1 else ''} "
                                "- integration is gate-blocked until committed")
            ui.html(
                '<table class="am-table">'
                f'<tr><th style="width:160px;">Last commit</th><td>{_esc(last or "(unknown)")}</td></tr>'
                f'<tr><th>Working tree</th><td><span class="{dot}">&#9679;</span> {state_label}</td></tr>'
                "</table>"
            )

        if dirty:
            with ui.element("div").classes("panel w-full").style("margin-bottom:16px;"):
                with ui.element("div").classes("panel-title"):
                    ui.html(f"<span>Uncommitted changes ({n_dirty})</span>")
                    ui.button("Commit AudioMirror", icon="commit",
                              on_click=lambda: _confirm_commit(n_dirty, content.refresh)) \
                        .props("unelevated dense color=primary size=sm")
                shown = dirty[:40]
                listing = "\n".join(shown)
                if n_dirty > len(shown):
                    listing += f"\n... and {n_dirty - len(shown)} more"
                ui.html(f'<div class="console" style="max-height:260px;">{_esc(listing)}</div>')
            ui.html('<p class="note">Committing snapshots the mirror state and unblocks the '
                    "exe's pre-integration gate. Commit only (never pushes); the message is editable "
                    "in the confirm dialog.</p>").classes("w-full")

    content()


def _confirm_commit(n_dirty: int, refresh) -> None:
    with ui.dialog() as dlg, ui.card().style(
            "background:var(--panel);color:var(--text);padding:20px;min-width:420px;gap:12px;"):
        ui.label("Commit AudioMirror?").style("font-weight:600;font-size:15px;")
        ui.label(
            f"Stages and commits all {n_dirty} change(s) in the AudioMirror repo "
            "(git add -A + git commit - local only, nothing is pushed)."
        ).classes("note").style("margin:0;")
        msg = ui.input("Commit message",
                       value=f"Mirror update ({n_dirty} changes, via GUI)") \
            .props("dense dark outlined").classes("w-full")
        with ui.row().classes("w-full justify-end").style("gap:10px;"):
            ui.button("Cancel", on_click=dlg.close).props("flat color=grey")

            async def go():
                dlg.close()
                await _do_commit(msg.value or "Mirror update (via GUI)", refresh)

            ui.button("Commit", on_click=go).props("unelevated color=primary")
    dlg.open()


async def _do_commit(message: str, refresh) -> None:
    code, out = await asyncio.to_thread(_git_result, "add", "-A")
    if code != 0:
        ui.notify(f"git add failed: {out[:300]}", type="negative", multi_line=True)
        return
    code, out = await asyncio.to_thread(_git_result, "commit", "-m", message)
    if code != 0:
        ui.notify(f"git commit failed: {out[:300]}", type="negative", multi_line=True)
        return
    ui.notify("AudioMirror committed - integration gate unblocked", type="positive")
    refresh()


def _esc(text: str) -> str:
    return text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")

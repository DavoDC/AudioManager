"""Mirror tab (skeleton) - READ-ONLY AudioMirror sync status.

git log / git status only; this tab (and the whole GUI) never writes to
AudioMirror. A 'Commit AudioMirror' action is the named next step required
to fully remove the terminal from the integration loop (the exe's
pre-integration gate blocks on an uncommitted mirror).
"""
from __future__ import annotations

import subprocess

from nicegui import ui

from gui import config


def _git(*args: str) -> str | None:
    try:
        out = subprocess.run(
            ["git", "-C", str(config.AUDIOMIRROR_REPO), *args],
            capture_output=True, text=True, encoding="utf-8", errors="replace",
            timeout=20,
        )
        return out.stdout.strip() if out.returncode == 0 else None
    except (OSError, subprocess.TimeoutExpired):
        return None


def build() -> None:
    with ui.element("header").classes("page"):
        ui.html("<h1>Mirror</h1>")
        ui.html('<div class="meta">AudioMirror sync status - read-only</div>')

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
                shown = dirty[:40]
                listing = "\n".join(shown)
                if n_dirty > len(shown):
                    listing += f"\n... and {n_dirty - len(shown)} more"
                ui.html(f'<div class="console" style="max-height:260px;">{_esc(listing)}</div>')
            with ui.element("div").classes("gap-note w-full"):
                ui.html(
                    "<b>To commit these changes today:</b> run "
                    '<span style="font-family:var(--font-mono)">git add -A &amp;&amp; git commit</span> '
                    "in the AudioMirror repo. A one-click <b>Commit AudioMirror</b> action here is "
                    "the planned next step for this tab (the last terminal dependency in the "
                    "integration loop) - deliberately not built yet: the GUI currently makes no "
                    "writes of any kind to AudioMirror."
                )

    content()


def _esc(text: str) -> str:
    return text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")

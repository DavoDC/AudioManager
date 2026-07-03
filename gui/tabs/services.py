"""Services tab (placeholder - TIER G5, far future by design).

Two feasibility stub cards per the brief; deliberately not built this round.
"""
from __future__ import annotations

from nicegui import ui


def build() -> None:
    with ui.element("header").classes("page"):
        ui.html("<h1>Services</h1>")
        ui.html('<div class="meta">TIER G5 - far future, feasibility stubs only</div>')

    with ui.element("div").classes("panel w-full").style("margin-bottom:14px;opacity:.75;"):
        ui.html(
            '<h3 style="margin:0 0 6px;font-size:14px;color:var(--text);">Last.fm read-only'
            '<span class="stretch-badge">STRETCH GOAL</span></h3>'
            '<div class="note" style="margin:0;">No OAuth needed - '
            '<span style="font-family:var(--font-mono)">user.getRecentTracks</span> and related '
            "read endpoints work with just a free API key. Lowest-effort real integration: recent "
            "scrobbles overlaid on the Library Browser, or a &quot;never scrobbled&quot; "
            "cross-reference panel.</div>"
        )
    with ui.element("div").classes("panel w-full").style("margin-bottom:14px;opacity:.75;"):
        ui.html(
            '<h3 style="margin:0 0 6px;font-size:14px;color:var(--text);">Spotify read-only'
            '<span class="stretch-badge">STRETCH GOAL</span></h3>'
            '<div class="note" style="margin:0;">Authorization Code + PKCE: an app registered in '
            "the Spotify Developer dashboard (David creates the registration), a "
            '<span style="font-family:var(--font-mono)">localhost</span> redirect, no client-secret '
            "storage. Higher effort than Last.fm - only after it, only with clear budget.</div>"
        )
    ui.html('<p class="note">Both remain far-future in docs/References/GUI-Architecture.md - an invitation to go further '
            "once the core tabs are polished, not a commitment.</p>")

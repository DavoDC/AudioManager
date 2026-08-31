"""Acquire tab - Stage 2 (Acquiring) of Music-Discovery-Workflow.md.

Sync Liked Songs -> inbox playlist, clickable Deemix links per track, and a
read-only Verify Downloads scan of NEWMUSIC_DIR. See "Acquire tab design" in
docs/References/GUI-Architecture.md. Cheap/MVP build (2026-08-31) - polish
items are tracked in IDEAS.md, not built here.
"""
from __future__ import annotations

import asyncio
import json
import sys
from pathlib import Path

from nicegui import ui

from gui import config

sys.path.insert(0, str(config.SPOTIFYGEN_ROOT))

_state = {"tracks": []}  # [(artist, title, url), ...] from the last fetch


def _load_last_playlist_id() -> str:
    if config.ACQUIRE_STATE_JSON.exists():
        try:
            return json.loads(config.ACQUIRE_STATE_JSON.read_text(encoding="utf-8")).get("playlist_id", "")
        except (json.JSONDecodeError, OSError):
            return ""
    return ""


def _save_last_playlist_id(playlist_id: str) -> None:
    config.CACHE_DIR.mkdir(parents=True, exist_ok=True)
    config.ACQUIRE_STATE_JSON.write_text(json.dumps({"playlist_id": playlist_id}), encoding="utf-8")


def _spotify_client():
    from src.config import load_config, CONFIG_PATH
    from src.spotify_client import RealSpotifyClient
    cfg = load_config(CONFIG_PATH)
    if not cfg:
        raise RuntimeError(f"SpotifyPlaylistGen config not found at {CONFIG_PATH}")
    return RealSpotifyClient(cfg)


def _do_sync_liked() -> str:
    from src.acquire import move_liked_to_playlist
    client = _spotify_client()
    result = move_liked_to_playlist(client)
    _save_last_playlist_id(result.playlist_id)
    msg = f"Moved {result.moved_count} track(s) to '{result.playlist_name}' ({result.playlist_id})"
    if result.errors:
        msg += f" - {len(result.errors)} error(s): {'; '.join(result.errors[:3])}"
    return msg


def _do_fetch_tracks(playlist_id_or_url: str) -> list[tuple[str, str, str]]:
    from src.open_playlist import extract_playlist_id, _build_deemix_url
    playlist_id = extract_playlist_id(playlist_id_or_url)
    client = _spotify_client()
    tracks = client.get_playlist_tracks(playlist_id)
    _save_last_playlist_id(playlist_id)
    return [(artist, title, _build_deemix_url(artist, title)) for artist, title in tracks]


def match_downloads(tracks: list[tuple[str, str]], newmusic_dir: Path) -> tuple[list[str], list[str]]:
    """Read-only fuzzy match of (artist, title) pairs against filenames already
    in newmusic_dir. Returns (found_labels, missing_labels). Pure/testable -
    no filesystem writes, matches gui/config.py's read-only NEWMUSIC_DIR contract."""
    from src.matcher import clean_artist, clean_title, normalise

    filenames = [p.stem for p in newmusic_dir.glob("*.mp3")] if newmusic_dir.is_dir() else []
    filename_words = [set(normalise(f).split()) for f in filenames]

    found, missing = [], []
    for artist, title in tracks:
        label = f"{artist} - {title}"
        track_words = set(normalise(f"{clean_artist(artist)} {clean_title(title)}").split())
        hit = any(
            track_words and fw and len(track_words & fw) / max(len(track_words), len(fw)) >= 0.5
            for fw in filename_words
        )
        (found if hit else missing).append(label)
    return found, missing


def build() -> None:
    with ui.element("header").classes("page"):
        ui.html("<h1>Acquire</h1>")
        ui.html('<div class="meta">Stage 2 - Liked Songs &rarr; inbox playlist &rarr; Deemix &rarr; NewMusic</div>')

    # Card 1 - sync liked songs
    with ui.element("div").classes("panel w-full").style("margin-bottom:16px;"):
        with ui.element("div").classes("panel-title"):
            ui.html("<span>Sync Liked Songs &rarr; Inbox Playlist</span>")
        result_label = ui.label("").classes("note")

        async def sync():
            result_label.set_text("Working...")
            try:
                msg = await asyncio.to_thread(_do_sync_liked)
                result_label.set_text(msg)
                ui.notify(msg, type="positive")
            except Exception as e:
                hint = ""
                if "403" in str(e):
                    hint = (" - likely your Spotify account isn't allowlisted for this app "
                            "(Development Mode apps require adding your account under "
                            "Users Management on https://developer.spotify.com/dashboard, "
                            "separate from OAuth scope consent)")
                result_label.set_text(f"Failed: {e}{hint}")
                ui.notify(f"Sync failed: {e}{hint}", type="negative", multi_line=True)

        ui.button("Move Liked Songs to Inbox", icon="playlist_add", on_click=sync) \
            .props("unelevated dense color=primary size=sm")
        ui.html('<p class="note" style="margin:6px 0 0;">First run needs a one-time Spotify browser '
                "consent (Liked Songs scope was just added).</p>")

    # Card 2 - fetch + open tracks
    with ui.element("div").classes("panel w-full").style("margin-bottom:16px;"):
        with ui.element("div").classes("panel-title"):
            ui.html("<span>Open Playlist Tracks</span>")
        playlist_input = ui.input("Playlist URL or ID", value=_load_last_playlist_id()) \
            .props("dense dark outlined").classes("w-full")

        @ui.refreshable
        def track_table():
            if not _state["tracks"]:
                ui.label("No tracks fetched yet.").classes("note")
                return
            rows = "".join(
                f'<tr><td>{_esc(a)}</td><td>{_esc(t)}</td>'
                f'<td><a href="#" onclick="window.open({json.dumps(u)},\'_blank\',\'noopener\');'
                f'return false;">Open in Deemix</a></td></tr>'
                for a, t, u in _state["tracks"]
            )
            ui.html(f'<table class="am-table"><tr><th>Artist</th><th>Title</th><th></th></tr>{rows}</table>') \
                .style("max-height:360px;overflow:auto;display:block;")

        async def fetch():
            try:
                _state["tracks"] = await asyncio.to_thread(_do_fetch_tracks, playlist_input.value or "")
                track_table.refresh()
                ui.notify(f"Fetched {len(_state['tracks'])} tracks", type="positive")
            except Exception as e:
                ui.notify(f"Fetch failed: {e}", type="negative", multi_line=True)

        def open_all():
            urls = [u for _, _, u in _state["tracks"]]
            # No setTimeout: a deferred call loses the click's "user gesture" status
            # and Chrome's popup blocker silently drops every tab after the first.
            # Firing window.open() synchronously for all of them keeps the gesture -
            # if the browser still blocks any, it shows a one-time "popups blocked"
            # icon in the address bar; click it -> Always allow for this site.
            js = ";".join(f"window.open({json.dumps(u)},'_blank','noopener')" for u in urls)
            ui.run_javascript(js)
            ui.notify(
                "If only one tab opened, click the blocked-popups icon in the address "
                "bar and choose 'Always allow' for this site.",
                type="info",
            )

        with ui.row().style("gap:8px;margin:8px 0;"):
            ui.button("Fetch Tracks", icon="download", on_click=fetch).props("dense outline size=sm")
            ui.button("Open All in Deemix", icon="open_in_new", on_click=open_all).props("dense outline size=sm")
        track_table()

    # Card 3 - verify downloads
    with ui.element("div").classes("panel w-full"):
        with ui.element("div").classes("panel-title"):
            ui.html("<span>Verify Downloads</span>")
        verify_label = ui.label("").classes("note")
        _missing: list[str] = []

        @ui.refreshable
        def missing_listing():
            if not _missing:
                return
            shown = _missing[:40]
            listing = "\n".join(shown)
            if len(_missing) > len(shown):
                listing += f"\n... and {len(_missing) - len(shown)} more"
            ui.html(f'<div class="console" style="max-height:220px;">{_esc(listing)}</div>')

        def verify():
            if not _state["tracks"]:
                verify_label.set_text("Fetch tracks first.")
                return
            found, missing = match_downloads([(a, t) for a, t, _ in _state["tracks"]], config.NEWMUSIC_DIR)
            verify_label.set_text(f"{len(found)}/{len(found) + len(missing)} downloaded")
            _missing[:] = missing
            missing_listing.refresh()

        ui.button("Check NewMusic Folder", icon="refresh", on_click=verify).props("dense outline size=sm")
        missing_listing()


def _esc(text: str) -> str:
    return text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")

"""Acquire tab - Stage 2 (Acquiring) of Music-Discovery-Workflow.md.

Fetch a playlist's tracks into a checklist table (artist/title/album/year/
length, per-row Deemix link, read-only Downloaded tickbox). The Downloaded column is
filled by a read-only fuzzy match against NEWMUSIC_DIR (see
match_downloads()), run once on fetch and then on a 2s poll timer
(_poll_downloads()) while the tab is open and tracks are loaded, so ticks
fill in as files land in NEWMUSIC_DIR with no manual button needed. See
"Acquire tab design" in docs/References/GUI-Architecture.md.
Sync Liked Songs is built but hidden (Spotify 403 - see _build_sync_liked_card).
Cheap/MVP build (2026-08-31, table redesign 2026-09-01, Verify Downloads card
merged into table 2026-09-01) - polish items are tracked in IDEAS.md, not built here.
"""
from __future__ import annotations

import asyncio
import json
import re
import sys
from pathlib import Path

from nicegui import ui

from gui import config

sys.path.insert(0, str(config.SPOTIFYGEN_ROOT))

_state = {"tracks": [], "downloaded": {}, "sort_col": None, "sort_reverse": False}  # tracks: [(artist, title, album, year, length, url), ...]; downloaded: {row_key: bool}

_SORT_COLUMNS = {"Artist": 0, "Title": 1, "Album": 2, "Year": 3, "Length": 4}


def _length_to_seconds(length: str) -> int:
    minutes, _, seconds = length.partition(":")
    try:
        return int(minutes) * 60 + int(seconds)
    except ValueError:
        return 0


def _sorted_tracks() -> list[tuple[str, str, str, str, str, str]]:
    """Sorted view of _state['tracks'] for display; original list (and its
    indices used in row_key) is left untouched so Downloaded-match keys stay stable."""
    col = _state["sort_col"]
    if col is None:
        return list(enumerate(_state["tracks"]))
    idx = _SORT_COLUMNS[col]
    if idx == 3:
        key = lambda row: (row[1][3] == "", row[1][3])  # Year: blanks last, else lexical (YYYY string)
    elif idx == 4:
        key = lambda row: _length_to_seconds(row[1][4])
    else:
        key = lambda row: row[1][idx].lower()
    return sorted(enumerate(_state["tracks"]), key=key, reverse=_state["sort_reverse"])


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
    """Deferred - see IDEAS.md TIER 2 'Sync Liked Songs broken (403)'. Card is
    hidden via _build_sync_liked_card() not being called; logic kept intact."""
    from src.acquire import move_liked_to_playlist
    client = _spotify_client()
    result = move_liked_to_playlist(client)
    _save_last_playlist_id(result.playlist_id)
    msg = f"Moved {result.moved_count} track(s) to '{result.playlist_name}' ({result.playlist_id})"
    if result.errors:
        msg += f" - {len(result.errors)} error(s): {'; '.join(result.errors[:3])}"
    return msg


def _format_duration(duration_ms: int) -> str:
    total_seconds = duration_ms // 1000
    return f"{total_seconds // 60}:{total_seconds % 60:02d}"


def _do_fetch_tracks(playlist_id_or_url: str) -> list[tuple[str, str, str, str, str, str]]:
    from src.open_playlist import extract_playlist_id, _build_deemix_url
    playlist_id = extract_playlist_id(playlist_id_or_url)
    client = _spotify_client()
    tracks = client.get_playlist_tracks_detailed(playlist_id)
    _save_last_playlist_id(playlist_id)
    return [
        (
            t["artist"], t["title"], t["album"], t["year"],
            _format_duration(t.get("duration_ms", 0)), _build_deemix_url(t["artist"], t["title"]),
        )
        for t in tracks
    ]


def match_downloads(tracks: list[tuple[str, str]], newmusic_dir: Path) -> tuple[list[str], list[str]]:
    """Read-only fuzzy match of (artist, title) pairs against filenames already
    in newmusic_dir. Returns (found_labels, missing_labels). Pure/testable -
    no filesystem writes, matches gui/config.py's read-only NEWMUSIC_DIR contract.

    Filenames follow the "Artist - Title.mp3" convention. Artist and title
    are matched separately - matching on the combined word set let a shared
    artist name swamp the ratio and produced false positives across an
    artist's whole catalogue (e.g. every Jack Harlow track ticking
    "downloaded" once one was). Artist match uses only the PRIMARY artist
    (text before " & "/";"/",") because a track's Spotify artist field can
    carry featured/collab artists ("DC The Don & Someone") that the
    downloaded filename never includes - case-insensitive substring either
    direction, not exact equality. Title match is normalised-exact, a
    substring either direction, or >=0.4 word overlap - looser than a strict
    equality/0.5-overlap check missed real matches (e.g. "DC THE DON -
    Yellow.mp3" not ticking for a fetched "DC The Don" / "Yellow" track)."""
    from src.matcher import clean_artist, clean_title, normalise

    def primary_artist(artist: str) -> str:
        return re.split(r"\s*&\s*|\s*;\s*|\s*,\s*", clean_artist(artist))[0]

    filename_parts = []
    if newmusic_dir.is_dir():
        for p in newmusic_dir.glob("*.mp3"):
            fa, _, ft = p.stem.partition(" - ")
            filename_parts.append((normalise(primary_artist(fa)), normalise(clean_title(ft))))

    found, missing = [], []
    for artist, title in tracks:
        label = f"{artist} - {title}"
        norm_artist = normalise(primary_artist(artist))
        norm_title = normalise(clean_title(title))
        title_words = set(norm_title.split())

        def artist_matches(fa: str) -> bool:
            return bool(norm_artist and fa) and (norm_artist in fa or fa in norm_artist)

        def title_matches(ft: str) -> bool:
            if not norm_title or not ft:
                return False
            if norm_title == ft or norm_title in ft or ft in norm_title:
                return True
            ft_words = set(ft.split())
            return bool(title_words and ft_words) and len(title_words & ft_words) / max(len(title_words), len(ft_words)) >= 0.4

        hit = any(artist_matches(fa) and title_matches(ft) for fa, ft in filename_parts)
        (found if hit else missing).append(label)
    return found, missing


def _build_sync_liked_card() -> None:
    """Deferred, not called from build() - Spotify 403s in Development Mode
    (account not allowlisted). See IDEAS.md TIER 2 'Sync Liked Songs broken'.
    Kept intact so re-enabling later is one line (call this from build())."""
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


def build() -> None:
    with ui.element("header").classes("page"):
        ui.html("<h1>Acquire</h1>")
        ui.html('<div class="meta">Stage 2 - Liked Songs &rarr; inbox playlist &rarr; Deemix &rarr; NewMusic</div>')

    # Card 2 - fetch + open tracks (Card 1, Sync Liked Songs, is hidden - see _build_sync_liked_card)
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
            with ui.element("table").classes("am-table acquire-table").style("width:100%;"):
                with ui.element("tr"):
                    for h in ("Artist", "Title", "Album", "Year", "Length", "Deemix", "Downloaded"):
                        with ui.element("th").style(
                            "text-align:center;" if h == "Downloaded" else "text-align:left;"
                        ):
                            if h in _SORT_COLUMNS:
                                arrow = ""
                                if _state["sort_col"] == h:
                                    arrow = " ▲" if not _state["sort_reverse"] else " ▼"

                                def _make_sort_handler(col=h):
                                    def handler():
                                        # cycle: unset -> asc -> desc -> unset (back to playlist order)
                                        if _state["sort_col"] != col:
                                            _state["sort_col"] = col
                                            _state["sort_reverse"] = False
                                        elif not _state["sort_reverse"]:
                                            _state["sort_reverse"] = True
                                        else:
                                            _state["sort_col"] = None
                                            _state["sort_reverse"] = False
                                        track_table.refresh()
                                    return handler

                                ui.label(h + arrow).style("cursor:pointer;").on("click", _make_sort_handler())
                            else:
                                ui.label(h)
                            if h == "Downloaded" and _state["downloaded"]:
                                found = sum(1 for v in _state["downloaded"].values() if v)
                                ui.label(f"{found}/{len(_state['downloaded'])}").classes("note") \
                                    .style("font-weight:normal;text-transform:none;")
                for i, (artist, title, album, year, length, url) in _sorted_tracks():
                    row_key = f"{i}:{artist}:{title}"
                    row_style = (
                        "background-color:rgba(64,150,255,0.08);"
                        if _state["downloaded"].get(row_key) else ""
                    )
                    with ui.element("tr").style(row_style):
                        with ui.element("td"):
                            ui.label(artist)
                        with ui.element("td"):
                            ui.label(title)
                        with ui.element("td"):
                            ui.label(album)
                        with ui.element("td"):
                            ui.label(year)
                        with ui.element("td"):
                            ui.label(length)
                        with ui.element("td"):
                            ui.link("Search", url, new_tab=True)
                        with ui.element("td").style("text-align:center;"):
                            ui.checkbox(value=_state["downloaded"].get(row_key, False)).props("disable")

        def _run_check_against_downloads():
            found, _missing = match_downloads(
                [(a, t) for a, t, _album, _year, _length, _url in _state["tracks"]], config.NEWMUSIC_DIR
            )
            found_set = set(found)
            _state["downloaded"] = {
                f"{i}:{artist}:{title}": f"{artist} - {title}" in found_set
                for i, (artist, title, _album, _year, _length, _url) in enumerate(_state["tracks"])
            }

        async def fetch():
            try:
                _state["tracks"] = await asyncio.to_thread(_do_fetch_tracks, playlist_input.value or "")
                await asyncio.to_thread(_run_check_against_downloads)
                track_table.refresh()
                ui.notify(f"Fetched {len(_state['tracks'])} tracks", type="positive")
            except Exception as e:
                ui.notify(f"Fetch failed: {e}", type="negative", multi_line=True)

        with ui.row().style("gap:8px;margin:8px 0;"):
            ui.button("Fetch Tracks", icon="download", on_click=fetch).props("dense outline size=sm")
        track_table()

        _poll = {"busy": False}

        async def _poll_downloads():
            """Re-runs the downloads match on a timer so ticked rows fill in
            without a manual button press while the tab sits open (2s between
            runs, next run only starts after the previous one finishes and the
            table only redraws when a match actually changed - a fresh mp3
            drops in mid-download, so this stays cheap rather than one-shot)."""
            if not _state["tracks"] or _poll["busy"]:
                return
            _poll["busy"] = True
            try:
                before = dict(_state["downloaded"])
                await asyncio.to_thread(_run_check_against_downloads)
                if _state["downloaded"] != before:
                    track_table.refresh()
            finally:
                _poll["busy"] = False

        ui.timer(2.0, _poll_downloads)

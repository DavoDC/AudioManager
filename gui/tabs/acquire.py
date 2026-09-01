"""Acquire tab - Stage 2 (Acquiring) of Music-Discovery-Workflow.md.

Fetch a playlist's tracks into a checklist table (artist/title/album/year/
length, per-row Deemix link, read-only Downloaded tickbox). The Downloaded column is
filled by a read-only fuzzy match against NEWMUSIC_DIR (see
match_downloads()), run once on fetch and then on a 2s poll timer
(_poll_downloads()) while the tab is open, so ticks fill in as files land in
NEWMUSIC_DIR with no manual button needed. A second, reversed scan
(find_extra_newmusic_files()) surfaces files already sitting in NEWMUSIC_DIR
that match no track in the loaded playlist (or every file, with no playlist
loaded) as yellow-highlighted rows appended below the fetched tracks, on the
same poll. Last-5 fetched playlists (id + name) persist to
config.ACQUIRE_STATE_JSON and are reachable via the history button next to
the playlist field. See "Acquire tab design" in docs/References/GUI-Architecture.md.
Sync Liked Songs is built but hidden (Spotify 403 - see _build_sync_liked_card).
Cheap/MVP build (2026-08-31, table redesign 2026-09-01, Verify Downloads card
merged into table 2026-09-01, NewMusic-surfacing/history/clear 2026-09-02) -
polish items are tracked in IDEAS.md, not built here.
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

_state = {"tracks": [], "downloaded": {}, "extra": [], "sort_col": None, "sort_reverse": False}
# tracks: [(artist, title, album, year, length, url), ...]; downloaded: {row_key: bool};
# extra: [(artist, title, url), ...] - files in NEWMUSIC_DIR matching no loaded track

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


_HISTORY_MAX = 5


def _load_state_json() -> dict:
    if config.ACQUIRE_STATE_JSON.exists():
        try:
            return json.loads(config.ACQUIRE_STATE_JSON.read_text(encoding="utf-8"))
        except (json.JSONDecodeError, OSError):
            return {}
    return {}


def _load_last_playlist_id() -> str:
    return _load_state_json().get("playlist_id", "")


def _load_history() -> list[dict]:
    """Last N playlists fetched, most-recent-first: [{"id", "name"}, ...].
    "name" may be "" if the name lookup failed - id still displays."""
    return _load_state_json().get("history", [])


def _save_last_playlist(playlist_id: str, name: str) -> None:
    """Persists playlist_id (back-compat for _load_last_playlist_id) plus a
    deduped, most-recent-first history capped at _HISTORY_MAX."""
    state = _load_state_json()
    history = [h for h in state.get("history", []) if h.get("id") != playlist_id]
    history.insert(0, {"id": playlist_id, "name": name})
    state["playlist_id"] = playlist_id
    state["history"] = history[:_HISTORY_MAX]
    config.CACHE_DIR.mkdir(parents=True, exist_ok=True)
    config.ACQUIRE_STATE_JSON.write_text(json.dumps(state), encoding="utf-8")


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
    _save_last_playlist(result.playlist_id, result.playlist_name)
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
    try:
        name = client.get_playlist_name(playlist_id)
    except Exception:
        name = ""
    _save_last_playlist(playlist_id, name)
    return [
        (
            t["artist"], t["title"], t["album"], t["year"],
            _format_duration(t.get("duration_ms", 0)), _build_deemix_url(t["artist"], t["title"]),
        )
        for t in tracks
    ]


def _primary_artist(artist: str, clean_artist) -> str:
    """Text before " & "/";"/"," - a Spotify track's artist field can carry
    featured/collab artists that a downloaded filename never includes."""
    return re.split(r"\s*&\s*|\s*;\s*|\s*,\s*", clean_artist(artist))[0]


def _scan_newmusic_filenames(newmusic_dir: Path) -> list[tuple[str, str, str, str]]:
    """Parses every "Artist - Title.mp3" file in newmusic_dir into
    (raw_artist, raw_title, norm_artist, norm_title). Shared by match_downloads
    (playlist -> filenames) and find_extra_newmusic_files (filenames -> playlist,
    the reverse direction) so both use identical normalisation."""
    from src.matcher import clean_artist, clean_title, normalise

    parts = []
    if newmusic_dir.is_dir():
        for p in newmusic_dir.glob("*.mp3"):
            fa, _, ft = p.stem.partition(" - ")
            parts.append((fa.strip(), ft.strip(), normalise(_primary_artist(fa, clean_artist)), normalise(clean_title(ft))))
    return parts


def _artist_title_match(norm_artist: str, norm_title: str, fa: str, ft: str) -> bool:
    """Loose match: primary-artist substring either direction (Spotify's artist
    field can carry featured/collab names a filename never includes), title
    normalised-exact/substring/>=0.4 word overlap (a strict equality/0.5-overlap
    check missed real matches like "DC THE DON - Yellow.mp3" against a fetched
    "DC The Don" / "Yellow" track)."""
    if not (norm_artist and fa) or not (norm_artist in fa or fa in norm_artist):
        return False
    if not norm_title or not ft:
        return False
    if norm_title == ft or norm_title in ft or ft in norm_title:
        return True
    title_words, ft_words = set(norm_title.split()), set(ft.split())
    return bool(title_words and ft_words) and len(title_words & ft_words) / max(len(title_words), len(ft_words)) >= 0.4


def match_downloads(tracks: list[tuple[str, str]], newmusic_dir: Path) -> tuple[list[str], list[str]]:
    """Read-only fuzzy match of (artist, title) pairs against filenames already
    in newmusic_dir. Returns (found_labels, missing_labels). Pure/testable -
    no filesystem writes, matches gui/config.py's read-only NEWMUSIC_DIR contract.

    Filenames follow the "Artist - Title.mp3" convention. Artist and title are
    matched separately - matching on the combined word set let a shared artist
    name swamp the ratio and produced false positives across an artist's whole
    catalogue (e.g. every Jack Harlow track ticking "downloaded" once one was).
    See _artist_title_match for the per-pair matching rule."""
    from src.matcher import clean_artist, clean_title, normalise

    filename_parts = _scan_newmusic_filenames(newmusic_dir)

    found, missing = [], []
    for artist, title in tracks:
        label = f"{artist} - {title}"
        norm_artist = normalise(_primary_artist(artist, clean_artist))
        norm_title = normalise(clean_title(title))
        hit = any(_artist_title_match(norm_artist, norm_title, fa, ft) for _, _, fa, ft in filename_parts)
        (found if hit else missing).append(label)
    return found, missing


def find_extra_newmusic_files(tracks: list[tuple[str, str]], newmusic_dir: Path) -> list[tuple[str, str]]:
    """Reverse of match_downloads: files already sitting in newmusic_dir that
    don't match ANY track in the currently loaded playlist (or every file, if
    no playlist is loaded). Surfaces files that were downloaded outright, or
    downloaded from a playlist this table was never pointed at. Returns
    [(artist, title), ...] parsed straight from each filename, read-only."""
    from src.matcher import clean_artist, clean_title, normalise

    track_norms = [(normalise(_primary_artist(a, clean_artist)), normalise(clean_title(t))) for a, t in tracks]

    extra = []
    for raw_artist, raw_title, norm_artist, norm_title in _scan_newmusic_filenames(newmusic_dir):
        if not any(_artist_title_match(ta, tt, norm_artist, norm_title) for ta, tt in track_norms):
            extra.append((raw_artist, raw_title))
    return extra


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
        with ui.row().style("gap:8px;align-items:center;flex-wrap:wrap;"):
            playlist_input = ui.input("Playlist URL or ID", value=_load_last_playlist_id()) \
                .props("dense dark outlined").style("width:360px;")

            def _apply_history_pick(playlist_id: str):
                playlist_input.value = playlist_id
                fetch()

            with ui.button(icon="history").props("dense outline size=sm"):
                with ui.menu() as history_menu:
                    @ui.refreshable
                    def history_items():
                        history = _load_history()
                        if not history:
                            ui.menu_item("No playlists fetched yet").props("disable")
                        for h in history:
                            label = h.get("name") or "(unnamed)"
                            ui.menu_item(f"{label} - {h['id']}", on_click=lambda pid=h["id"]: _apply_history_pick(pid))
                    history_menu.on("show", history_items.refresh)
                    history_items()

        @ui.refreshable
        def track_table():
            if not _state["tracks"] and not _state["extra"]:
                ui.label("No tracks fetched yet, and nothing sitting unmatched in NewMusic.").classes("note")
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

                if _state["extra"]:
                    with ui.element("tr").classes("batch-header"):
                        with ui.element("td").props("colspan=7"):
                            ui.label(f"IN NEWMUSIC, NOT IN THIS PLAYLIST ({len(_state['extra'])})")
                    for artist, title, url in sorted(_state["extra"], key=lambda r: r[0].lower()):
                        with ui.element("tr").style("background-color:rgba(242,184,75,0.10);"):
                            with ui.element("td"):
                                ui.label(artist)
                            with ui.element("td"):
                                ui.label(title)
                            with ui.element("td"):
                                ui.label("")
                            with ui.element("td"):
                                ui.label("")
                            with ui.element("td"):
                                ui.label("")
                            with ui.element("td"):
                                ui.link("Search", url, new_tab=True)
                            with ui.element("td").style("text-align:center;"):
                                ui.checkbox(value=True).props("disable")

        def _run_check_against_downloads():
            from src.open_playlist import _build_deemix_url
            current_tracks = [(a, t) for a, t, _album, _year, _length, _url in _state["tracks"]]
            found, _missing = match_downloads(current_tracks, config.NEWMUSIC_DIR)
            found_set = set(found)
            _state["downloaded"] = {
                f"{i}:{artist}:{title}": f"{artist} - {title}" in found_set
                for i, (artist, title, _album, _year, _length, _url) in enumerate(_state["tracks"])
            }
            _state["extra"] = [
                (artist, title, _build_deemix_url(artist, title))
                for artist, title in find_extra_newmusic_files(current_tracks, config.NEWMUSIC_DIR)
            ]

        async def fetch():
            try:
                _state["tracks"] = await asyncio.to_thread(_do_fetch_tracks, playlist_input.value or "")
                await asyncio.to_thread(_run_check_against_downloads)
                track_table.refresh()
                history_items.refresh()
                ui.notify(f"Fetched {len(_state['tracks'])} tracks", type="positive")
            except Exception as e:
                ui.notify(f"Fetch failed: {e}", type="negative", multi_line=True)

        def clear():
            _state["tracks"] = []
            _state["downloaded"] = {}
            playlist_input.value = ""
            _run_check_against_downloads()
            track_table.refresh()

        with ui.row().style("gap:8px;margin:8px 0;"):
            ui.button("Fetch Tracks", icon="download", on_click=fetch).props("dense outline size=sm")
            ui.button("Clear", icon="clear", on_click=clear).props("dense outline size=sm color=grey")
        # Runs once synchronously on tab build so NewMusic-only files ("extra"
        # rows) surface immediately, even before any playlist has been fetched.
        _run_check_against_downloads()
        track_table()

        _poll = {"busy": False}

        async def _poll_downloads():
            """Re-runs the downloads match (and the NewMusic-only "extra" scan)
            on a timer so ticked rows and surfaced extras fill in without a
            manual button press while the tab sits open (2s between runs, next
            run only starts after the previous one finishes and the table only
            redraws when something actually changed - a fresh mp3 drops in
            mid-download, so this stays cheap rather than one-shot). Runs even
            with no playlist loaded, so extras still update."""
            if _poll["busy"]:
                return
            _poll["busy"] = True
            try:
                before = (dict(_state["downloaded"]), list(_state["extra"]))
                await asyncio.to_thread(_run_check_against_downloads)
                if (_state["downloaded"], _state["extra"]) != before:
                    track_table.refresh()
            finally:
                _poll["busy"] = False

        ui.timer(2.0, _poll_downloads)

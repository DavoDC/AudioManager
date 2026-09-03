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
the playlist field. A "Hide downloaded" checkbox next to Fetch/Clear filters
already-ticked rows out of the fetched-tracks view only (extra rows are
untouched). A segmented progress bar above the panel (progress_bar()) gives
an at-a-glance blue/grey/yellow read of downloaded/missing/extra counts,
refreshed alongside track_table() on fetch, clear, and the poll timer. See
"Acquire tab design" in docs/References/GUI-Architecture.md and docs/DESIGN.md
for the GUI's visual-design rules.
Sync Liked Songs is built but hidden (Spotify 403 - see _build_sync_liked_card).
Cheap/MVP build (2026-08-31, table redesign 2026-09-01, Verify Downloads card
merged into table 2026-09-01, NewMusic-surfacing/history/clear 2026-09-02,
hide-downloaded toggle 2026-09-02) - polish items are tracked in IDEAS.md,
not built here.
"""
from __future__ import annotations

import asyncio
import json
import re
import sys
from pathlib import Path

from nicegui import ui

from gui import config

sys.path.insert(0, str(config.SPOTIFYGEN_ROOT / "src"))

_state = {"tracks": [], "downloaded": {}, "extra": [], "sort_col": None, "sort_reverse": False,
          "hide_downloaded": False, "playlist_loaded": False}
# tracks: [(artist, title, album, year, length, url), ...]; downloaded: {row_key: bool};
# extra: [(artist, title, url, path), ...] - files in NEWMUSIC_DIR matching no loaded track;
# path is the file's own Path, read by _read_mp3_tags() for Album/Year/Length at render time
# hide_downloaded: when True, track_table() skips rows already ticked Downloaded
# playlist_loaded: True once fetch() has successfully populated tracks from a real
# playlist, False from init and after clear() - distinguishes "extra" rows meaning
# "not accounted for by the loaded playlist" (diff framing) from "browsing an empty
# NewMusic folder with nothing loaded yet" (browse framing) - see _extra_segment_label
# and _extra_batch_header, which brand only the label text, never the underlying count.

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
    from spotify_tools.config import load_config, CONFIG_PATH
    from spotify_tools.spotify_client import RealSpotifyClient
    cfg = load_config(CONFIG_PATH)
    if not cfg:
        raise RuntimeError(f"SpotifyTools config not found at {CONFIG_PATH}")
    return RealSpotifyClient(cfg)


def _do_sync_liked() -> str:
    """Deferred - see IDEAS.md TIER 2 'Sync Liked Songs broken (403)'. Card is
    hidden via _build_sync_liked_card() not being called; logic kept intact."""
    from spotify_tools.acquire import move_liked_to_playlist
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
    from spotify_tools.open_playlist import extract_playlist_id, _build_deemix_url
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


def _read_mp3_tags(path: Path) -> tuple[str, str, str]:
    """Read-only (album, year, length) straight from an mp3's own ID3 tags,
    mirroring gui/art.py's read-only mutagen precedent for album art. Used
    only for "extra" NewMusic rows (files with no matching playlist track,
    so there's no Spotify API data to show Album/Year/Length from instead -
    the file's own tags are the only source). Never raises: missing file,
    missing tags, or an unreadable/corrupt mp3 all fall back to blanks so a
    single bad file never breaks the row render."""
    album = year = length_str = ""
    try:
        from mutagen.easyid3 import EasyID3
        tags = EasyID3(str(path))
        album = (tags.get("album") or [""])[0]
        year = (tags.get("date") or [""])[0][:4]
    except Exception:
        pass
    try:
        from mutagen.mp3 import MP3
        length_str = _format_duration(int(MP3(str(path)).info.length * 1000))
    except Exception:
        pass
    return album, year, length_str


def _primary_artist(artist: str, clean_artist) -> str:
    """Text before " & "/";"/"," - a Spotify track's artist field can carry
    featured/collab artists that a downloaded filename never includes."""
    return re.split(r"\s*&\s*|\s*;\s*|\s*,\s*", clean_artist(artist))[0]


def _scan_newmusic_filenames(newmusic_dir: Path) -> list[tuple[str, str, str, str, Path]]:
    """Parses every "Artist - Title.mp3" file in newmusic_dir into
    (raw_artist, raw_title, norm_artist, norm_title, path). Shared by match_downloads
    (playlist -> filenames) and find_extra_newmusic_files (filenames -> playlist,
    the reverse direction) so both use identical normalisation. The path is
    carried through so extra rows can read the file's own ID3 tags (see
    _read_mp3_tags) - never used for matching, only display."""
    from spotify_tools.matcher import clean_artist, clean_title, normalise

    parts = []
    if newmusic_dir.is_dir():
        for p in newmusic_dir.glob("*.mp3"):
            fa, _, ft = p.stem.partition(" - ")
            parts.append((fa.strip(), ft.strip(), normalise(_primary_artist(fa, clean_artist)), normalise(clean_title(ft)), p))
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
    from spotify_tools.matcher import clean_artist, clean_title, normalise

    filename_parts = _scan_newmusic_filenames(newmusic_dir)

    found, missing = [], []
    for artist, title in tracks:
        label = f"{artist} - {title}"
        norm_artist = normalise(_primary_artist(artist, clean_artist))
        norm_title = normalise(clean_title(title))
        hit = any(_artist_title_match(norm_artist, norm_title, fa, ft) for _, _, fa, ft, _ in filename_parts)
        (found if hit else missing).append(label)
    return found, missing


def find_extra_newmusic_files(tracks: list[tuple[str, str]], newmusic_dir: Path) -> list[tuple[str, str, Path]]:
    """Reverse of match_downloads: files already sitting in newmusic_dir that
    don't match ANY track in the currently loaded playlist (or every file, if
    no playlist is loaded). Surfaces files that were downloaded outright, or
    downloaded from a playlist this table was never pointed at. Returns
    [(artist, title, path), ...] parsed straight from each filename plus the
    file's own Path (read-only, never used for matching - lets callers read
    the file's real ID3 tags for Album/Year/Length, see _read_mp3_tags)."""
    from spotify_tools.matcher import clean_artist, clean_title, normalise

    track_norms = [(normalise(_primary_artist(a, clean_artist)), normalise(clean_title(t))) for a, t in tracks]

    extra = []
    for raw_artist, raw_title, norm_artist, norm_title, path in _scan_newmusic_filenames(newmusic_dir):
        if not any(_artist_title_match(ta, tt, norm_artist, norm_title) for ta, tt in track_norms):
            extra.append((raw_artist, raw_title, path))
    return extra


def _extra_segment_label(count: int, playlist_loaded: bool) -> str:
    """progress_bar()'s third-segment text. With no playlist loaded,
    find_extra_newmusic_files() correctly returns literally every NewMusic
    file (there's nothing to diff against yet) - labelling that "extra"
    reads as an anomaly, so this branches to plain browse-mode wording.
    Once a playlist has been fetched, the diff framing is accurate and kept."""
    if playlist_loaded:
        return f"{count} extra in NewMusic"
    return f"{count} files in NewMusic"


def _extra_batch_header(count: int, playlist_loaded: bool) -> str:
    """Batch-header row above the extra-rows table section. Same diff-vs-browse
    distinction as _extra_segment_label - see its docstring."""
    if playlist_loaded:
        return f"IN NEWMUSIC, NOT IN THIS PLAYLIST ({count})"
    return f"NEWMUSIC FOLDER ({count})"


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

    @ui.refreshable
    def progress_bar():
        """At-a-glance segmented bar: blue = playlist tracks already in
        NewMusic (Downloaded), grey = playlist tracks still missing, yellow =
        files in NewMusic that match no track in the loaded playlist (extra).
        Segment widths are proportional to counts; a flat grey bar (0 tracks,
        0 extra) means nothing has been fetched or scanned yet."""
        downloaded = sum(1 for v in _state["downloaded"].values() if v)
        missing = len(_state["tracks"]) - downloaded
        extra = len(_state["extra"])
        total = downloaded + missing + extra
        segments = [
            (downloaded, "var(--accent)", "#0c0e13", f"{downloaded} downloaded"),
            (missing, "#3a3f4d", "var(--text-dim)", f"{missing} missing"),
            (extra, "var(--accent3)", "#0c0e13", _extra_segment_label(extra, _state["playlist_loaded"])),
        ]
        with ui.element("div").style(
            "background:var(--panel);border:1px solid var(--panel-border);"
            "border-radius:var(--radius-panel);padding:10px 14px;margin-bottom:16px;width:100%;"
        ):
            if not total:
                ui.label("Fetch a playlist to see progress").classes("note").style("margin:0;")
                return
            with ui.element("div").style(
                "height:26px;border-radius:var(--radius-pill);overflow:hidden;"
                "display:flex;background:#3a3f4d;width:100%;"
            ):
                for count, bg, fg, label in segments:
                    if not count:
                        continue
                    pct = count / total * 100
                    # Labels only fit legibly once a segment is wide enough - a
                    # narrower slice (e.g. a handful of extras next to 60+
                    # downloaded) shows the count as a native title tooltip
                    # instead of squeezing/wrapping text into a sliver.
                    seg = ui.element("div").style(
                        f"width:{pct}%;background:{bg};height:100%;display:flex;"
                        "align-items:center;justify-content:center;overflow:hidden;"
                        f"font-size:11px;font-weight:600;color:{fg};white-space:nowrap;"
                    )
                    if pct >= 12:
                        with seg:
                            ui.label(label)
                    else:
                        seg.props(f'title="{label}"')

    progress_bar()

    # Card 2 - fetch + open tracks (Card 1, Sync Liked Songs, is hidden - see _build_sync_liked_card)
    with ui.element("div").classes("panel w-full").style("margin-bottom:16px;"):
        with ui.element("div").classes("panel-title"):
            ui.html("<span>Open Playlist Tracks</span>")
        with ui.row().style("gap:8px;align-items:center;flex-wrap:wrap;"):
            playlist_input = ui.input("Playlist URL or ID", value=_load_last_playlist_id()) \
                .props("dense dark outlined").style("width:360px;")

            def _apply_history_pick(playlist_id: str):
                playlist_input.value = playlist_id
                asyncio.create_task(fetch())

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
                for i, (artist, title, album, year, length, url) in _sorted_tracks():
                    row_key = f"{i}:{artist}:{title}"
                    is_downloaded = _state["downloaded"].get(row_key, False)
                    if _state["hide_downloaded"] and is_downloaded:
                        continue
                    row_style = "background-color:rgba(91,140,255,0.08);" if is_downloaded else ""
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
                            ui.label(_extra_batch_header(len(_state["extra"]), _state["playlist_loaded"]))
                    for artist, title, url, path in sorted(_state["extra"], key=lambda r: r[0].lower()):
                        if _state["hide_downloaded"]:
                            continue
                        album, year, length = _read_mp3_tags(path)
                        with ui.element("tr").style("background-color:rgba(242,184,75,0.10);"):
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
                                ui.checkbox(value=True).props("disable")

        def _run_check_against_downloads():
            from spotify_tools.open_playlist import _build_deemix_url
            current_tracks = [(a, t) for a, t, _album, _year, _length, _url in _state["tracks"]]
            found, _missing = match_downloads(current_tracks, config.NEWMUSIC_DIR)
            found_set = set(found)
            _state["downloaded"] = {
                f"{i}:{artist}:{title}": f"{artist} - {title}" in found_set
                for i, (artist, title, _album, _year, _length, _url) in enumerate(_state["tracks"])
            }
            _state["extra"] = [
                (artist, title, _build_deemix_url(artist, title), path)
                for artist, title, path in find_extra_newmusic_files(current_tracks, config.NEWMUSIC_DIR)
            ]

        async def fetch():
            try:
                _state["tracks"] = await asyncio.to_thread(_do_fetch_tracks, playlist_input.value or "")
                _state["playlist_loaded"] = True
                await asyncio.to_thread(_run_check_against_downloads)
                track_table.refresh()
                progress_bar.refresh()
                history_items.refresh()
                ui.notify(f"Fetched {len(_state['tracks'])} tracks", type="positive")
            except Exception as e:
                ui.notify(f"Fetch failed: {e}", type="negative", multi_line=True)

        def clear():
            _state["tracks"] = []
            _state["downloaded"] = {}
            _state["sort_col"] = None
            _state["sort_reverse"] = False
            _state["playlist_loaded"] = False
            playlist_input.value = ""
            _run_check_against_downloads()
            track_table.refresh()
            progress_bar.refresh()
            history_items.refresh()

        def _toggle_hide_downloaded(e):
            _state["hide_downloaded"] = e.value
            track_table.refresh()

        with ui.row().style("gap:8px;margin:8px 0;align-items:center;"):
            ui.button("Fetch Tracks", icon="download", on_click=fetch).props("dense outline size=sm")
            ui.button("Clear", icon="clear", on_click=clear).props("dense outline size=sm color=grey")
            ui.checkbox("Hide downloaded", value=_state["hide_downloaded"], on_change=_toggle_hide_downloaded) \
                .props("dense").classes("note").style("margin:0;padding:0;")
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
                    progress_bar.refresh()
            finally:
                _poll["busy"] = False

        ui.timer(2.0, _poll_downloads)

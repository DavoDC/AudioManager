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
the playlist field. That same file also caches the fetched tracks and their
Downloaded ticks per playlist id (_save_tracks_cache/_load_tracks_cache), so
a browser reload or a tab rebuild restores the table via
restore_cached_tracks() instead of starting empty; Clear forgets that cache
(_forget_cached_playlist) but keeps the history. A "Hide downloaded" checkbox next to Fetch/Clear filters
already-ticked rows out of the fetched-tracks view only (extra rows are
untouched). A segmented progress bar above the panel (progress_bar()) gives
an at-a-glance blue/grey/yellow read of downloaded/missing/extra counts,
refreshed alongside track_table() on fetch, clear, and the poll timer. See
"Acquire tab design" in docs/References/GUI-Architecture.md and docs/DESIGN.md
for the GUI's visual-design rules.
Sync Liked Songs is built but hidden (Spotify 403 - see _build_sync_liked_card).
"Simulate (sample data)" next to Fetch Tracks loads synthetic tracks/extras
(see _sample_tracks/_sample_extra/simulate()) with no Spotify client call and
no NEWMUSIC_DIR read, so the tab can be exercised end to end - downloaded,
missing and extra rows all present - without a real playlist or NewMusic
folder; a "simulate-banner" (mirrors integration.py's) marks the state as
synthetic until a real Fetch or Clear resets it.
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
          "hide_downloaded": False, "playlist_loaded": False, "simulated": False}
# tracks: [(artist, title, album, year, length, url), ...]; downloaded: {row_key: bool};
# extra: [(artist, title, url, path), ...] - files in NEWMUSIC_DIR matching no loaded track;
# path is the file's own Path, read by _read_mp3_tags() for Album/Year/Length at render time
# hide_downloaded: when True, track_table() skips rows already ticked Downloaded
# playlist_loaded: True once fetch() has successfully populated tracks from a real
# playlist, False from init and after clear() - distinguishes "extra" rows meaning
# "not accounted for by the loaded playlist" (diff framing) from "browsing an empty
# NewMusic folder with nothing loaded yet" (browse framing) - see _extra_segment_label
# and _extra_batch_header, which brand only the label text, never the underlying count.
# simulated: True once simulate() has loaded synthetic sample data instead of a real
# fetch(); reset False by clear() and by a real fetch(), mirroring integration.py's
# IntegrationState.simulated. Drives the "simulate-banner" in build().

_SORT_COLUMNS = {"Artist": 0, "Title": 1, "Album": 2, "Year": 3, "Length": 4}

_HISTORY_BUTTON_TOOLTIP = "Playlist history"
# Icon-only history button next to the playlist input carried no title/tooltip -
# see IDEAS.md "Playlist-history icon-button has no tooltip".

_DEEMIX_LINK_LABEL = "Open in Deemix"
# Per-row link used to read just "Search" on every row, giving no hint what it
# opens - see IDEAS.md "Per-row 'Search' link label is context-free".

_LIKED_SONGS_ID = "__liked_songs__"
_LIKED_SONGS_NAME = "Liked Songs"
# Liked Songs has no playlist id of its own, but every persistence path
# (_save_last_playlist/_save_tracks_cache/_load_tracks_cache, and therefore
# history and restore_cached_tracks) is keyed by playlist id - so Liked
# Songs is given this sentinel to stand in as its "playlist id" rather than
# building a second, parallel persistence mechanism. A real Spotify playlist
# id is base62 (letters/digits only), so this can never collide with one.
# _apply_history_pick() and the playlist-input default both special-case
# this id so it never leaks into a real playlist fetch by accident.

_refresh_hooks = {"track_table": lambda: None, "progress_bar": lambda: None,
                   "history_items": lambda: None, "simulate_banner": lambda: None}
# Wired to the real @ui.refreshable closures by build() once they exist. simulate()
# is a module-level function (so it's directly unit-testable, like _do_fetch_tracks)
# but still needs to trigger a UI refresh after setting _state - these default no-ops
# let it run standalone in a test with no live NiceGUI page, same trick as
# IntegrationState.refresh's default no-op in integration.py.


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


def _write_state_json(state: dict) -> None:
    config.CACHE_DIR.mkdir(parents=True, exist_ok=True)
    config.ACQUIRE_STATE_JSON.write_text(json.dumps(state), encoding="utf-8")


def _save_tracks_cache(playlist_id: str, tracks: list, downloaded: dict) -> None:
    """Persists the fetched tracks and their Downloaded ticks to
    config.ACQUIRE_STATE_JSON under {"cache": {playlist_id: {...}}}, so a
    browser reload or a tab rebuild restores the table instead of starting
    empty (see restore_cached_tracks). One mechanism serves both the "state
    persistence across page reload" and "cache fetched tracks to disk" gaps.

    Cache entries are pruned to the playlists still in history, so the file
    can never grow without bound as playlists come and go. Tracks are stored
    as JSON lists and come back as tuples (see _load_tracks_cache)."""
    if not playlist_id:
        return
    state = _load_state_json()
    cache = state.get("cache", {})
    if not isinstance(cache, dict):
        cache = {}
    cache[playlist_id] = {"tracks": [list(t) for t in tracks], "downloaded": dict(downloaded)}
    keep = {h.get("id") for h in state.get("history", [])} | {playlist_id}
    state["cache"] = {pid: entry for pid, entry in cache.items() if pid in keep}
    _write_state_json(state)


def _load_tracks_cache(playlist_id: str) -> tuple[list[tuple], dict]:
    """Inverse of _save_tracks_cache. Returns ([], {}) for an unknown playlist
    id, an absent/corrupt state file, or a malformed entry - a bad cache must
    degrade to "nothing restored", never break the tab build."""
    if not playlist_id:
        return [], {}
    entry = (_load_state_json().get("cache") or {}).get(playlist_id)
    if not isinstance(entry, dict):
        return [], {}
    tracks = [tuple(t) for t in entry.get("tracks", []) if isinstance(t, (list, tuple))]
    downloaded = entry.get("downloaded", {})
    return tracks, downloaded if isinstance(downloaded, dict) else {}


def _forget_cached_playlist() -> None:
    """Drops the current playlist id and its cached tracks - what Clear needs
    so a rebuilt tab genuinely starts blank instead of restoring what was just
    cleared. History is deliberately left intact: Clear resets the view, it
    does not erase where you have been."""
    state = _load_state_json()
    playlist_id = state.get("playlist_id", "")
    state["playlist_id"] = ""
    cache = state.get("cache")
    if isinstance(cache, dict):
        cache.pop(playlist_id, None)
        state["cache"] = cache
    _write_state_json(state)


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


def _do_fetch_liked_tracks() -> list[tuple[str, str, str, str, str, str]]:
    """Liked Songs counterpart of _do_fetch_tracks(): identical row shape and
    downstream flow (match_downloads/find_extra_newmusic_files run on the
    result exactly the same way), but sourced from RealSpotifyClient's
    get_liked_tracks_detailed() instead of a playlist id. Persists under
    _LIKED_SONGS_ID so history/cache/restore all work unmodified."""
    from spotify_tools.open_playlist import _build_deemix_url
    client = _spotify_client()
    tracks = client.get_liked_tracks_detailed()
    _save_last_playlist(_LIKED_SONGS_ID, _LIKED_SONGS_NAME)
    return [
        (
            t["artist"], t["title"], t["album"], t["year"],
            _format_duration(t.get("duration_ms", 0)), _build_deemix_url(t["artist"], t["title"]),
        )
        for t in tracks
    ]


def _sample_tracks() -> list[tuple[str, str, str, str, str, str]]:
    """Synthetic sample data for Simulate mode - matches the (artist, title,
    album, year, length, url) shape _do_fetch_tracks() returns, so simulate()
    can drop this straight into _state["tracks"]. Mirrors integration.py's
    _sample_entries() precedent."""
    from spotify_tools.open_playlist import _build_deemix_url
    rows = [
        ("Bring Me The Horizon", "Doomed", "Post Human: Survival Horror", "2020", "3:12"),
        ("Dua Lipa", "Levitating", "Future Nostalgia", "2020", "3:23"),
        ("Kendrick Lamar", "HUMBLE.", "DAMN.", "2017", "2:57"),
        ("Polo G", "Gang With Me", "The Goat", "2020", "3:04"),
        ("Fred again..", "Delilah (pull me out of this)", "Actual Life 3", "2022", "3:33"),
        ("Jack Harlow", "Lonesome", "Jackman.", "2023", "2:15"),
    ]
    return [
        (artist, title, album, year, length, _build_deemix_url(artist, title))
        for artist, title, album, year, length in rows
    ]


def _sample_extra() -> list[tuple[str, str, str, Path]]:
    """Synthetic sample data for Simulate mode - matches the (artist, title,
    url, path) shape _state["extra"] holds (see _run_check_against_downloads).
    Paths are fabricated and never need to exist on disk: _read_mp3_tags()
    already degrades to blanks for a missing/unreadable file, so this also
    exercises that exact fallback at render time. First row deliberately
    shares artist/title with a sample track simulate() marks downloaded, to
    exercise the "downloaded" row landing next to an "extra" row for the same
    song; the rest match no sample track, i.e. genuinely extra."""
    from spotify_tools.open_playlist import _build_deemix_url
    rows = [
        ("Bring Me The Horizon", "Doomed"),
        ("Tame Impala", "The Less I Know The Better"),
        ("Metro Boomin", "Space Cadet"),
    ]
    return [
        (artist, title, _build_deemix_url(artist, title), Path(f"NewMusic/{artist} - {title}.mp3"))
        for artist, title in rows
    ]


def simulate() -> None:
    """Simulate mode: loads synthetic sample tracks/extras entirely in
    memory - no _spotify_client() call, no NEWMUSIC_DIR read - so the
    diff-mode table, progress bar and every row state (downloaded, missing,
    extra) can be exercised without a real Spotify account or a real
    NewMusic folder. Mirrors integration.py's run_simulate() precedent.
    Browse mode (no playlist loaded) already has a working empty/default
    state and doesn't need synthetic data, so this always lands in diff
    mode (playlist_loaded=True), the more complex/common path."""
    tracks = _sample_tracks()
    extra = _sample_extra()
    downloaded_labels = {"Bring Me The Horizon - Doomed", "Fred again.. - Delilah (pull me out of this)"}
    _state["tracks"] = tracks
    _state["downloaded"] = {
        f"{i}:{artist}:{title}": f"{artist} - {title}" in downloaded_labels
        for i, (artist, title, *_rest) in enumerate(tracks)
    }
    _state["extra"] = extra
    _state["playlist_loaded"] = True
    _state["simulated"] = True
    _refresh_hooks["track_table"]()
    _refresh_hooks["progress_bar"]()
    _refresh_hooks["history_items"]()
    _refresh_hooks["simulate_banner"]()


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


def _run_check_against_downloads() -> None:
    """Recomputes _state["downloaded"] and _state["extra"] from a read-only
    scan of config.NEWMUSIC_DIR, then persists the result for the loaded
    playlist. Module level (not a build() closure) so it is directly
    unit-testable and so clear()/restore both reuse the one implementation."""
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
    if _state["playlist_loaded"] and not _state["simulated"]:
        _save_tracks_cache(_load_last_playlist_id(), _state["tracks"], _state["downloaded"])


def restore_cached_tracks() -> bool:
    """Reloads the last playlist's tracks/ticks from disk into _state on tab
    build, so a browser reload or a tab rebuild no longer starts empty.
    Returns True when something was restored. Never overwrites live state:
    a session that already has tracks (or is in Simulate mode) is left alone."""
    if _state["tracks"] or _state["simulated"]:
        return False
    tracks, downloaded = _load_tracks_cache(_load_last_playlist_id())
    if not tracks:
        return False
    _state["tracks"] = tracks
    _state["downloaded"] = downloaded
    _state["playlist_loaded"] = True
    return True


def clear_tab_state() -> None:
    """Clear button's whole effect apart from blanking the input box: resets
    every piece of _state to a blank slate (sort order included), forgets the
    cached playlist so a rebuild does not restore it, rescans NewMusic so the
    extra-files section survives the clear, and refreshes all four panels -
    history included, matching what fetch() does."""
    _state["tracks"] = []
    _state["downloaded"] = {}
    _state["sort_col"] = None
    _state["sort_reverse"] = False
    _state["playlist_loaded"] = False
    _state["simulated"] = False
    _forget_cached_playlist()
    _run_check_against_downloads()
    _refresh_hooks["track_table"]()
    _refresh_hooks["progress_bar"]()
    _refresh_hooks["history_items"]()
    _refresh_hooks["simulate_banner"]()


def _poll_should_skip() -> bool:
    """Guard for _poll_downloads()'s early return: while _state["simulated"]
    is True, the 2s poll must not touch _state at all, or simulate()'s
    synthetic downloaded/extra data gets silently clobbered by a real
    NEWMUSIC_DIR scan within a couple of poll cycles (mirrors integration.py's
    `if not S.simulated:` guard around its own background/real-execution
    logic). Extracted to module level - unlike _poll_downloads itself, which
    is a closure nested in build() and needs a live NiceGUI page - so the
    guard condition is directly unit-testable."""
    return _state["simulated"]


_LABEL_MIN_PCT = 12.0
# A segment narrower than this can't show its label legibly (text squeezes or
# wraps into a sliver), so progress_bar() renders the label as a native title
# tooltip instead - see _segment_shows_label.


def progress_metrics(downloaded: int, missing: int, extra: int) -> dict:
    """Pure segment arithmetic behind progress_bar() - extracted so the
    percentage maths is unit-testable rather than only eyeballable in the UI.

    total: all three counts, the denominator for segment widths.
    pct_complete: the headline "N%" above the bar - downloaded as a share of
    the PLAYLIST only (downloaded + missing); extra files are in NewMusic but
    are not part of the loaded playlist, so counting them would let unrelated
    files inflate (or deflate) playlist completion.
    widths: [downloaded, missing, extra] as percentages of total, summing to
    100 when total > 0 and all zero when nothing has been fetched or scanned."""
    total = downloaded + missing + extra
    playlist_total = downloaded + missing
    return {
        "total": total,
        "pct_complete": round(downloaded / playlist_total * 100) if playlist_total else 0,
        "widths": [count / total * 100 if total else 0.0 for count in (downloaded, missing, extra)],
    }


def _segment_shows_label(width_pct: float) -> bool:
    """True when a segment is wide enough to render its label inline; False
    means progress_bar() falls back to a title tooltip. See _LABEL_MIN_PCT."""
    return width_pct >= _LABEL_MIN_PCT


def _pct_label(pct: int) -> str:
    """Headline percentage text above the bar. Spelled out as "N% of playlist"
    rather than bare "N%" because pct_complete and the bar's own segment
    widths use different denominators (pct excludes extra files by design,
    widths include them) - see IDEAS.md "Progress bar's headline percentage
    and its visual segment widths use different denominators". Labelling the
    number explicitly is the cheapest fix; the underlying math is unchanged."""
    return f"{pct}% of playlist"


def _header_label(col: str, sort_col: str | None, sort_reverse: bool) -> str:
    """Sortable-column header text. The active column shows its direction
    arrow (unchanged); an inactive sortable column now also carries a faint
    up/down glyph so sortability is discoverable before the first click - see
    IDEAS.md "Sortable table headers give no visual hint that they're
    clickable". Only called for columns already in _SORT_COLUMNS."""
    if sort_col == col:
        return col + (" ▼" if sort_reverse else " ▲")
    return col + " ⇅"


def _downloaded_cell_text(is_downloaded: bool) -> str:
    """Read-only Downloaded-column cell text, replacing a disabled checkbox
    (which read as "broken control", not "status") - see IDEAS.md "Read-only
    'Downloaded' status shown as a disabled checkbox". A downloaded row gets
    a checkmark pill (see .dl-badge in theme.py); a missing row gets a plain
    dash - there is nothing to flag, so it earns no badge."""
    return "✓ Downloaded" if is_downloaded else "-"


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
    def simulate_banner():
        if _state["simulated"]:
            ui.html('<div class="simulate-banner">SIMULATED - sample data, no real playlist fetched, '
                    "no NewMusic folder scanned.</div>")

    simulate_banner()
    _refresh_hooks["simulate_banner"] = simulate_banner.refresh

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
        metrics = progress_metrics(downloaded, missing, extra)
        total = metrics["total"]
        segments = [
            (downloaded, "var(--accent)", "#0c0e13", f"{downloaded} downloaded"),
            (missing, "var(--track-missing)", "var(--text-dim)", f"{missing} missing"),
            (extra, "var(--accent3)", "#0c0e13", _extra_segment_label(extra, _state["playlist_loaded"])),
        ]
        with ui.element("div").style(
            "background:var(--panel);border:1px solid var(--panel-border);"
            "border-radius:var(--radius-panel);padding:10px 14px;margin-bottom:16px;width:100%;"
        ):
            if not total:
                ui.label("Fetch a playlist to see progress").classes("note").style("margin:0;")
                return
            with ui.row().style("justify-content:flex-end;margin-bottom:4px;"):
                ui.label(_pct_label(metrics["pct_complete"])).classes("note").style("margin:0;color:var(--text-dim);")
            with ui.element("div").style(
                "height:26px;border-radius:var(--radius-pill);overflow:hidden;"
                "display:flex;background:#3a3f4d;width:100%;"
            ):
                for (count, bg, fg, label), pct in zip(segments, metrics["widths"]):
                    if not count:
                        continue
                    # Labels only fit legibly once a segment is wide enough - a
                    # narrower slice (e.g. a handful of extras next to 60+
                    # downloaded) shows the count as a native title tooltip
                    # instead of squeezing/wrapping text into a sliver.
                    seg = ui.element("div").style(
                        f"width:{pct}%;background:{bg};height:100%;display:flex;"
                        "align-items:center;justify-content:center;overflow:hidden;"
                        f"font-size:11px;font-weight:600;color:{fg};white-space:nowrap;"
                    )
                    if _segment_shows_label(pct):
                        with seg:
                            ui.label(label)
                    else:
                        seg.props(f'title="{label}"')

    progress_bar()
    _refresh_hooks["progress_bar"] = progress_bar.refresh

    # Card 2 - fetch + open tracks (Card 1, Sync Liked Songs, is hidden - see _build_sync_liked_card)
    with ui.element("div").classes("panel w-full").style("margin-bottom:16px;"):
        with ui.element("div").classes("panel-title"):
            ui.html("<span>Open Playlist Tracks</span>")
            ui.html(
                '<span class="col-legend">'
                '<span><span class="dot" style="background:var(--accent);"></span>Downloaded</span>'
                '<span><span class="dot" style="background:var(--accent3);"></span>Extra in NewMusic</span>'
                "</span>"
            )
        with ui.row().style("gap:8px;align-items:center;flex-wrap:wrap;"):
            _last_id = _load_last_playlist_id()
            playlist_input = ui.input(
                "Playlist URL or ID", value=_last_id if _last_id != _LIKED_SONGS_ID else ""
            ).props("dense dark outlined").style("width:520px;")
            # A prior Load Liked Songs leaves _LIKED_SONGS_ID as the persisted
            # "current" id (see _do_fetch_liked_tracks) - shown here it would
            # read as a bogus playlist id and, if left in the box, get fed
            # straight to fetch() as one. Blank the box instead; restore_cached_tracks()
            # still brings the Liked Songs table back regardless of this box's value.

            def _apply_history_pick(playlist_id: str):
                if playlist_id == _LIKED_SONGS_ID:
                    asyncio.create_task(load_liked())
                    return
                playlist_input.value = playlist_id
                asyncio.create_task(fetch())

            with ui.button(icon="history").props(f'dense outline size=sm title="{_HISTORY_BUTTON_TOOLTIP}"'):
                with ui.menu() as history_menu:
                    @ui.refreshable
                    def history_items():
                        current_id = _load_last_playlist_id()
                        history = [h for h in _load_history() if h.get("id") != current_id]
                        if not history:
                            ui.menu_item("No playlists fetched yet").props("disable")
                            return
                        ui.menu_item("OTHER PLAYLISTS").props("disable").classes("text-caption")
                        for h in history:
                            label = h.get("name") or "(unnamed)"
                            text = label if h["id"] == _LIKED_SONGS_ID else f"{label} - {h['id']}"
                            ui.menu_item(text, on_click=lambda pid=h["id"]: _apply_history_pick(pid))
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
                                label_text = _header_label(h, _state["sort_col"], _state["sort_reverse"])

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

                                ui.label(label_text).style("cursor:pointer;").on("click", _make_sort_handler())
                            else:
                                ui.label(h)
                for i, (artist, title, album, year, length, url) in _sorted_tracks():
                    row_key = f"{i}:{artist}:{title}"
                    is_downloaded = _state["downloaded"].get(row_key, False)
                    if _state["hide_downloaded"] and is_downloaded:
                        continue
                    with ui.element("tr").classes("row-downloaded" if is_downloaded else ""):
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
                            ui.link(_DEEMIX_LINK_LABEL, url, new_tab=True)
                        with ui.element("td").style("text-align:center;"):
                            cell = ui.label(_downloaded_cell_text(is_downloaded))
                            if is_downloaded:
                                cell.classes("dl-badge")

                if _state["extra"] and _state["playlist_loaded"]:
                    with ui.element("tr").classes("batch-header"):
                        with ui.element("td").props("colspan=7"):
                            ui.label(_extra_batch_header(len(_state["extra"]), _state["playlist_loaded"]))
                if _state["extra"]:
                    for artist, title, url, path in sorted(_state["extra"], key=lambda r: r[0].lower()):
                        if _state["hide_downloaded"]:
                            continue
                        album, year, length = _read_mp3_tags(path)
                        with ui.element("tr").classes("row-extra"):
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
                                ui.link(_DEEMIX_LINK_LABEL, url, new_tab=True)
                            with ui.element("td").style("text-align:center;"):
                                ui.label(_downloaded_cell_text(True)).classes("dl-badge")

        async def fetch():
            try:
                _state["tracks"] = await asyncio.to_thread(_do_fetch_tracks, playlist_input.value or "")
                _state["playlist_loaded"] = True
                _state["simulated"] = False  # a real fetch always supersedes a prior Simulate run
                await asyncio.to_thread(_run_check_against_downloads)
                track_table.refresh()
                progress_bar.refresh()
                history_items.refresh()
                simulate_banner.refresh()
                ui.notify(f"Fetched {len(_state['tracks'])} tracks", type="positive")
            except Exception as e:
                ui.notify(f"Fetch failed: {e}", type="negative", multi_line=True)

        async def load_liked():
            """Liked Songs counterpart of fetch(): same downstream flow (match
            against NEWMUSIC_DIR, persist, refresh table/progress/history),
            sourced from _do_fetch_liked_tracks() instead of a playlist id -
            see that function's docstring for the persistence-key rationale."""
            try:
                _state["tracks"] = await asyncio.to_thread(_do_fetch_liked_tracks)
                _state["playlist_loaded"] = True
                _state["simulated"] = False
                await asyncio.to_thread(_run_check_against_downloads)
                track_table.refresh()
                progress_bar.refresh()
                history_items.refresh()
                simulate_banner.refresh()
                ui.notify(f"Loaded {len(_state['tracks'])} liked songs", type="positive")
            except Exception as e:
                ui.notify(f"Load Liked Songs failed: {e}", type="negative", multi_line=True)

        def clear():
            playlist_input.value = ""
            clear_tab_state()

        def _toggle_hide_downloaded(e):
            _state["hide_downloaded"] = e.value
            track_table.refresh()

        with ui.row().style("gap:8px;margin:8px 0;align-items:center;"):
            ui.button("Fetch Tracks", icon="download", on_click=fetch).props("dense outline size=sm")
            ui.button("Load Liked Songs", icon="favorite", on_click=load_liked).props("dense outline size=sm")
            ui.button("Simulate (sample data)", icon="science", on_click=simulate) \
                .props("dense outline size=sm color=grey")
            ui.button("Clear", icon="clear", on_click=clear).props("dense outline size=sm color=grey")
            ui.checkbox("Hide downloaded", value=_state["hide_downloaded"], on_change=_toggle_hide_downloaded) \
                .props("dense").classes("note").style("margin:0;padding:0;")
        # Restore the last playlist's tracks/ticks from disk (see
        # restore_cached_tracks) so a browser reload or a tab rebuild comes
        # back to the table you left, then run the NewMusic scan once
        # synchronously so extra rows and Downloaded ticks are current
        # immediately, even before any playlist has been fetched.
        restore_cached_tracks()
        _run_check_against_downloads()
        track_table()
        _refresh_hooks["track_table"] = track_table.refresh
        _refresh_hooks["history_items"] = history_items.refresh

        _poll = {"busy": False}

        async def _poll_downloads():
            """Re-runs the downloads match (and the NewMusic-only "extra" scan)
            on a timer so ticked rows and surfaced extras fill in without a
            manual button press while the tab sits open (2s between runs, next
            run only starts after the previous one finishes and the table only
            redraws when something actually changed - a fresh mp3 drops in
            mid-download, so this stays cheap rather than one-shot). Runs even
            with no playlist loaded, so extras still update. Skips entirely
            while _state["simulated"] is True - see _poll_should_skip()."""
            if _poll["busy"] or _poll_should_skip():
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

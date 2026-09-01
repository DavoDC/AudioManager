# AudioManager GUI Architecture

Reference doc: design vision, stack decisions, and third-party libraries for the GUI layer. Open GUI work items live in `docs/Development/IDEAS.md` (tagged `[GUI]`) - this file has no backlog, no checkboxes, nothing to action. Completed build history: `docs/Development/HISTORY.md`. Fable build brief: `docs/Development/fable-gui/fable-brief.md`.

---

## CLI Feature Parity - What the GUI Replicates

| CLI Feature | GUI Equivalent | Notes |
|-------------|---|---|
| `audiomanager integrate` (dry-run, confirm/decline per file) | Integration tab with per-track decision blocks | Visual queue + album art + routing preview |
| `audioManager tag-fix` (define + apply correction rules) | TagFix panel - define rules, apply to library or NewMusic batches | Rules-based bulk operation, NOT individual track editor. No mp3tag-style metadata editor. |
| `audioManager stats` (library analysis) | Statistics Dashboard tab | Charts + distribution analysis |
| `audioManager sync` (read library state) | Mirror tab (shows last sync state) | Display AudioMirror status without requiring CLI |
| `audioManager search` / filtering | Library Browser with search + filters | Full-text search, filter by genre/decade/artist |
| Batch operations (integrate or fix-tag N files at once) | Integration tab + TagFix panel with batch apply | Apply decisions/rules to multiple tracks in sequence |
| Automation / cron jobs | **CLI remains** - not replicated in GUI | Users who need headless automation use CLI directly |

**Key principle:** GUI is not a replacement for CLI. It's an alternative interface for interactive use. Power users and automation scripts continue using CLI; casual users prefer GUI.

---

## Design Vision

**Sonarr/Radarr-style:** Tabs on the left sidebar, content pane on the right. Clean, functional, data-dense.

**Startup behaviour:** On launch, read the existing JSON contract instantly (no subprocess call, no writes) and populate all stats panels with "last run X ago" plus a Re-run button - see Architecture Decisions below for why auto-run-on-launch was rejected.

**Tab order as built:** Statistics -> Integration -> TagFix -> Library Browser -> Mirror -> Services (Spotify/Last.fm/cross-synthesis, far future).

**Tab order, next addition:** Acquire slots in right after Statistics - see "Acquire tab design" below - because Stage 2 (Acquiring) is the first action David actually takes each session, before Library Browser or Integration are relevant.

---

## Architecture Decisions

**Subprocess + JSON contract. Core-library extraction AND daemon both explicitly deferred (decided 2026-07-03, Fable build session).**
The reuse worry behind extracting a shared core library (Python re-implementing parsing/stats and drifting from the C# implementation) is solved at the data layer instead: `analysis --json-output` emits `logs/analysis-stats.json` (aggregate stats, computed by the C# Analyser's own StatList primitives) and `logs/tracks.json` (full per-track array). The GUI shells out to the exe and reads those files - zero duplicated logic, zero XML parsing in Python, enforced by code not discipline. A daemon buys nothing for a single-user local tool (process lifecycle, IPC surface, crash recovery) when "run the exe, read the JSON it wrote" is crash-proof and fast (warm-cache analysis over the full library completes in seconds). Revisit only if a future tier needs real-time push (a Services-tab live scrobble feed) - a daemon/websocket can then be ADDED as another JSON consumer without touching this path.

**Stack: NiceGUI** (Python-native, FastAPI + Vue/Quasar under the hood, ECharts via `ui.echart`). Pure-Python event handlers wire subprocess calls and file reads directly; no JS build step; every chart type the spec needs (donut/pie/treemap/bar/radar/gauge). Code lives in top-level `gui/`, fully separate from the C# solution.

**Real file sizes:** `summary.totalLibraryBytes` / `avgFileBytes` come from a single disk walk inside the exe and arrive in the stats JSON - no bitrate-based estimation in Python. Per-track exact size still has no cheap source, so the Library Browser deliberately has no Size column.

**Deferred, unchanged:** REST API layer (add later only if an external consumer appears).

**Resolved (2026-08-31):** SpotifyTools generalization - decided NOT to extract a shared library. The Acquire tab imports SpotifyPlaylistGen's `SpotifyInterface`/`RealSpotifyClient` directly (same `sys.path.insert` pattern `open_playlist.py` already uses for its own sibling imports), same language, same process, no new abstraction layer. Revisit only if a second consumer of Spotify data appears (e.g. Services' cross-synthesis view) and the duplication actually shows up.

**GUI does not auto-run analysis at startup** - deviation from the original brief. It loads the existing JSON instantly and shows "last run X ago" with a Re-run button. Rationale: instant startup, no surprise subprocess/mirror writes on every launch, staleness fully visible to the user. Easy to flip if this turns out to be the wrong call.

---

## Build status (as of the 2026-07-03 Fable session)

All six tabs exist in `gui/` (NiceGUI, launched via `scripts/launch-gui-dev.bat`). See `gui/README.md`.

- **Statistics - FULL.** Stat tiles with vs-last-batch deltas, genre donut/pie/treemap swap, decade bar/donut, year top-N/show-all, genre radar, top artists excl/all toggle, batch-grouped recent additions, per-batch bar chart (AudioMirror git history is the canonical batch source), age buckets + callout, cover-resolution histogram, tag-completeness and hi-res-cover rings, global date window, freshness controls (Re-run analysis / Force full regen with confirm; force-regen passes `--no-auto-commit` and routes mirror changes to the Mirror tab).
- **Integration - FULL (selective execution).** Staged scan -> review queue (real album art, destination, reason, tag-change chips, badges, per-track accept/decline) -> confirm -> structured per-track progress. Declined tracks are excluded via `integrate --manifest <accepted.json> --no-input` (added 2026-07-03): the GUI writes the accepted set to `gui/.cache/accepted-manifest.json`, the exe filters before scan-ahead/duplicate review, and manifests match by raw dry-run filename OR the canonical TagFixer rename so they survive renames between dry and real runs.
- **Library Browser - MVP.** `tracks.json` rows, search + genre/decade chips, column picker, table/grid with real mutagen-extracted covers (page-lazy, cached), server-side pagination.
- **Tag Fix - skeleton.** Cards document the exe's real fixed transforms; Run Fixed Rules = `tagfix --dry-run`. Open gap: configurable rules need a C# change (tracked in IDEAS.md).
- **Mirror - functional.** Status/dirty listing + one-click Commit AudioMirror (confirm dialog, editable message, local commit only - added 2026-07-03). The GUI's only AudioMirror write; everything else stays read-only.
- **Services - placeholder.** Two stub cards, deliberately not built - see Services design below.
- **Acquire - MVP (2026-08-31).** Sync Liked Songs -> Inbox playlist, fetch-and-open Deemix links per track (staggered `window.open`, no more Enter/'q' loop), read-only Verify Downloads scan of `NEWMUSIC_DIR`. Cheap build, no caching layer for fetched tracks beyond the last-used playlist id (`gui/.cache/acquire-state.json`), no manual per-track match override. Polish items: `IDEAS.md` TIER 2/3.

**Visual system (2026-07-03):** fluid motion layer (pointer-tracking spotlight via delegated JS + CSS vars, hover lift, staggered entrances, tab transitions, reduced-motion respect) plus a **mood-reactive theme** - the dominant genre in `analysis-stats.json` tints the accent system at startup (`theme.apply_mood`, genre->palette map in `gui/theme.py`) with a "Mood" chip in the nav. Chart colors bind at import time, so `apply_mood` must run before tab modules import (enforced in `gui/main.py`).

---

## Acquire tab design (2026-08-31, replaces Stage 2 of Music-Discovery-Workflow.md)

Automates the mechanical half of Stage 2 (Acquiring) from `docs/References/Music-Discovery-Workflow.md`; the judgment half (which tracks to like on Spotify, which Deemix search result to pick) stays manual - see the `/think` writeup in the commit that introduced this section for the full reasoning. Reuses SpotifyPlaylistGen and `gui.config.NEWMUSIC_DIR` (already read-only by convention) directly - no new subprocess/CLI/JSON-contract layer, since both sides are Python in the same process family (see "Resolved" note above).

**Card 1 - Sync Liked Songs -> Inbox playlist.** One button. Calls a new `SpotifyPlaylistGen.src.acquire.move_liked_to_playlist()` (reads all liked tracks, gets-or-creates a playlist named `AudioManager Inbox`, adds the tracks, only then clears liked songs). Requires the `user-library-read`/`user-library-modify` scopes added to `SCOPES` in `spotify_client.py` - the cached OAuth token's scope no longer matches so spotipy reprompts for consent automatically on first use; this is a one-time manual browser step, not something Claude or the GUI can do for David.

**Card 2 - Open playlist tracks.** Input defaults to the last-used inbox playlist id (cached in `gui/.cache/acquire-state.json`, mirroring `data/playlist_cache/` on the SpotifyPlaylistGen side). Fetch renders a table of tracks, each row a clickable "Open in Deemix" link built via SpotifyPlaylistGen's existing `_clean_for_search`/`_open_in_manager` logic (primary-artist-only, feat-stripped) - imported, not reimplemented, so behavior matches `open_playlist.py` exactly. An "Open All" button fires the links in a staggered JS loop instead of the old one-by-one Enter/'q' prompt.

**Verify downloads (merged into the track table, 2026-09-01).** Was a standalone Card 3; now a read-only "Downloaded" tickbox column on the Open Playlist Tracks table, filled by a "Check Against Downloads" button. Still the same scan of `gui.config.NEWMUSIC_DIR` for `*.mp3`, fuzzy-matched against the fetched track list via SpotifyPlaylistGen's `matcher.py` (`clean_title`/`clean_artist`/`normalise`) through the same `match_downloads()` function - no matching logic changed, only the presentation. Shows a found/total summary count; the old missing-tracks text listing was dropped (see IDEAS.md). Directly answers the gap the 2026-04-26 Stage 2 post-mortem hit (28-vs-126 track discrepancy, no automated way to check).

**Explicitly out of scope:** driving Deemix's own search/result-selection (that's the human judgment call the whole pipeline exists to protect); any Stage 1 (Discovery) automation - no real leverage point exists there, liking tracks on Spotify already is the interface.

---

## Dev mode: hot-reload (2026-09-01)

**Always on via `scripts\launch-gui-dev.bat`** (`AM_GUI_WATCH=1`, windowless -
pythonw, no console). There is a single launcher; every launch runs the
watcher. Ported from
StreamPilot's `src/hot_reload.py` (same contract, same tests): `gui/hot_reload.py`
polls every `.py` file under `gui/` once a second (stdlib only, no watchdog dep).

**Two ways a restart triggers - use the explicit one when building a feature:**
- **Explicit "reload now" signal (preferred):** touch/create
  `gui/.cache/reload.trigger` (any content, even empty) - the very next poll
  consumes it (deletes the file) and restarts immediately. This is the mechanism
  for a deliberate multi-file/multi-minute build: keep editing across several
  files for as long as needed, then touch the trigger once everything is wired
  up. From Claude's Bash tool: `touch "C:/Users/David/GitHubRepos/AudioManager/gui/.cache/reload.trigger"`.
- **Passive fallback (1 hour debounce):** if nobody signals, the watcher
  eventually restarts on its own once the file set has gone quiet that long -
  purely a "forgot to touch the trigger" backstop.

Either path is gated by a syntax check (`compile()`, no bytecode-cache side
effect) before actually restarting - a half-written edit that doesn't parse
just keeps the old (working) process running, re-checked every poll, instead of
restarting into a guaranteed crash. This does NOT catch a semantic/runtime bug -
verify the change actually works in the browser after a risky edit, not just at
the end. Restart is `os.execv(sys.executable, sys.argv)` (whole-process re-exec,
same args) - reloads all code, not just one module, since Python doesn't
hot-reload imported modules on its own.

**No custom browser-reload JS needed, unlike StreamPilot's dashboard:** NiceGUI's
own socket.io client already reloads the page (`window.location.reload()`) when
its websocket disconnects and a subsequent reconnect/handshake doesn't match the
old session (`nicegui/static/nicegui.js`, `connect_error`/`try_reconnect`/
`finish_handshake` handlers) - a backend restart naturally triggers this, so the
open browser tab picks up the new process on its own within a couple seconds of
the restart, no extra wiring on the AudioManager side.

**Host is `localhost`, not `127.0.0.1`** (`ui.run(host="localhost", ...)` in
`gui/main.py`) - unlike StreamPilot's hand-rolled `http.server`, NiceGUI's
`ui.run` uses the same `host=` value for both the uvicorn bind and the
auto-opened browser URL, so there's no need for StreamPilot's bind-vs-display
split; setting `host` alone covers both.

---

## Services tab design (far future)

- **Spotify tab** - integrate SpotifyPlaylistGen (or a generalized SpotifyTools lib, if that's decided before building this): playlist generator from the offline library, cross-reference offline tracks vs Spotify availability, recently played on Spotify.
- **Last.fm tab** - scrobble history and listening stats: top tracks/artists (weekly, monthly, all-time), play counts overlaid on Library Browser, listening trends over time.
- **Cross-synthesis view** - overlay all data sources: what's owned offline but not on Spotify, tracks with zero Last.fm scrobbles (never listened), a unified ownership + listening picture.

Backlog entry: `IDEAS.md` `[GUI] Services tab data sources`.

---

## Architecture diagram

```
┌─────────────────────────────────────────────┐
│  User Interfaces                            │
│  ┌────────────────┐  ┌────────────────┐   │
│  │  CLI (scripts, │  │  GUI (NiceGUI) │   │
│  │   automation)  │  │  (interactive) │   │
│  └────────┬───────┘  └────────┬───────┘   │
└───────────┼────────────────────┼───────────┘
            │                    │
            ▼                    ▼
┌─────────────────────────────────────────────┐
│  (Optional, deferred) REST API Layer         │
└─────────────────┬───────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────┐
│  AudioManager.exe (C#)                       │
│  - routing logic                            │
│  - tagging / metadata correction            │
│  - integration workflow                     │
│  - AudioMirror XML parsing & writing        │
│  - library analysis & stats                 │
│  - JSON contract: analysis-stats.json,      │
│    tracks.json                              │
└─────────────────────────────────────────────┘
```

**Design principles:**
- AudioMirror XMLs are the source of truth. The GUI never reads or writes them directly - only through the exe's JSON contract.
- No duplicate XML parsing, routing logic, or tag-fixing code in Python.
- GUI is read-only except through the exe's own existing write paths (integration, tag-fix, mirror commit).
- AudioManager = the product name. DWave was a working name, dropped.

### Third-party library candidates (for future audio features)

| Library | Purpose | How to get |
|---------|---------|------------|
| TagLibSharp | Read/write ID3, Vorbis, APE, FLAC tags - already in use | NuGet `taglib-sharp` |
| NAudio | Audio playback, waveform analysis, format conversion (C#) | NuGet `NAudio` |
| FFprobe (subprocess) | Extract metadata from any audio format via CLI | `FFprobe.exe` - no C# lib needed |
| AcoustID + MusicBrainz | Acoustic fingerprinting - identify tracks by sound | `acoustid.net` |

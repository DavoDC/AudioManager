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

---

## Architecture Decisions

**Subprocess + JSON contract. Core-library extraction AND daemon both explicitly deferred (decided 2026-07-03, Fable build session).**
The reuse worry behind extracting a shared core library (Python re-implementing parsing/stats and drifting from the C# implementation) is solved at the data layer instead: `analysis --json-output` emits `logs/analysis-stats.json` (aggregate stats, computed by the C# Analyser's own StatList primitives) and `logs/tracks.json` (full per-track array). The GUI shells out to the exe and reads those files - zero duplicated logic, zero XML parsing in Python, enforced by code not discipline. A daemon buys nothing for a single-user local tool (process lifecycle, IPC surface, crash recovery) when "run the exe, read the JSON it wrote" is crash-proof and fast (warm-cache analysis over the full library completes in seconds). Revisit only if a future tier needs real-time push (a Services-tab live scrobble feed) - a daemon/websocket can then be ADDED as another JSON consumer without touching this path.

**Stack: NiceGUI** (Python-native, FastAPI + Vue/Quasar under the hood, ECharts via `ui.echart`). Pure-Python event handlers wire subprocess calls and file reads directly; no JS build step; every chart type the spec needs (donut/pie/treemap/bar/radar/gauge). Code lives in top-level `gui/`, fully separate from the C# solution.

**Real file sizes:** `summary.totalLibraryBytes` / `avgFileBytes` come from a single disk walk inside the exe and arrive in the stats JSON - no bitrate-based estimation in Python. Per-track exact size still has no cheap source, so the Library Browser deliberately has no Size column.

**Deferred, unchanged:** REST API layer (add later only if an external consumer appears); SpotifyTools generalization (decide before building any Spotify tab - see Services design below).

**GUI does not auto-run analysis at startup** - deviation from the original brief. It loads the existing JSON instantly and shows "last run X ago" with a Re-run button. Rationale: instant startup, no surprise subprocess/mirror writes on every launch, staleness fully visible to the user. Easy to flip if this turns out to be the wrong call.

---

## Build status (as of the 2026-07-03 Fable session)

All six tabs exist in `gui/` (NiceGUI, launched via `scripts/launch-gui.bat`). See `gui/README.md`.

- **Statistics - FULL.** Stat tiles with vs-last-batch deltas, genre donut/pie/treemap swap, decade bar/donut, year top-N/show-all, genre radar, top artists excl/all toggle, batch-grouped recent additions, per-batch bar chart (AudioMirror git history is the canonical batch source), age buckets + callout, cover-resolution histogram, tag-completeness and hi-res-cover rings, global date window, freshness controls (Re-run analysis / Force full regen with confirm; force-regen passes `--no-auto-commit` and routes mirror changes to the Mirror tab).
- **Integration - FULL (accept-all execution).** Staged scan -> review queue (real album art, destination, reason, tag-change chips, badges, per-track accept/decline) -> confirm -> structured per-track progress. Open gap: per-track SELECTIVE execution needs an `integrate --manifest <accepted.json> --no-input` exe mode - the review queue is already shaped to drive it (tracked in IDEAS.md).
- **Library Browser - MVP.** `tracks.json` rows, search + genre/decade chips, column picker, table/grid with real mutagen-extracted covers (page-lazy, cached), server-side pagination.
- **Tag Fix - skeleton.** Cards document the exe's real fixed transforms; Run Fixed Rules = `tagfix --dry-run`. Open gap: configurable rules need a C# change (tracked in IDEAS.md).
- **Mirror - skeleton.** Read-only status/dirty listing. Open gap: one-click Commit AudioMirror action (tracked in IDEAS.md).
- **Services - placeholder.** Two stub cards, deliberately not built - see Services design below.

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

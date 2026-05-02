# AudioManager GUI Roadmap

Planning doc for the GUI layer of AudioManager. CLI work lives in IDEAS.md. GUI is a separate development lifecycle - do not start until CLI TIER 1 is stable.

---

## CLI Feature Parity - What the GUI Must Replicate

**Goal:** The GUI should eventually offer every major workflow the CLI provides, but with a cleaner, more visual interface.

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

**Startup behaviour:** On launch, automatically read AudioMirror XMLs (no regeneration, no writes) and populate all stats panels. Data is always fresh. User opens the app and sees their library immediately.

**Tab order (build in sequence, not in parallel):**

| Tab | Priority | Description |
|-----|----------|-------------|
| Statistics | TIER G1 - start here | Charts, genre/decade distribution, library overview |
| Integration | TIER G2 | NewMusic routing, confirm/decline, run integration from GUI |
| TagFix | TIER G2b | Define tag-correction rules, apply to library or NewMusic batches (not individual track editor) |
| Library Browser | TIER G3 | Browse artists/albums/tracks, search, filter |
| Mirror | TIER G4 | AudioMirror sync status, diff view, commit log |
| Services | TIER G5 (far future) | Spotify, Last.fm, cross-synthesis |

---

## TIER G0 - Decisions (Before Writing Code)

- [ ] **CLI vs GUI vs Both - Architecture Decision** (**CRITICAL**)
  - **Option 1 (recommended): Keep both. CLI + GUI consume a shared core library.**
    - Extract AudioManager business logic into a library (no CLI, no GUI). All routing, tagging, integration logic lives here.
    - CLI: thin wrapper around the library. Remains for scripting, automation, batch operations, headless use (cron jobs, deployment pipelines).
    - GUI: another thin wrapper around the same library. For interactive exploration and manual decisions.
    - **Benefit:** Single source of truth. Bug fixes apply everywhere. Users choose their interface (CLI for power users, GUI for discovery). Future-proof: can add API, plugins, other UIs later.
    - **Trade-off:** Upfront refactor to extract library. But pays dividends across the product lifetime.
  - **Option 2 (not recommended): GUI only, deprecate CLI.**
    - Simpler short-term, but loses scripting/automation value. Breaks existing workflows. Non-interactive use cases fail.
  - **Option 3 (not recommended): Separate CLI and GUI codebases.**
    - Both implement their own routing/tagging logic. Duplication = maintenance burden, divergent bugs, sync nightmare.
  - **Recommendation:** Go with Option 1. Refactor to extract a core library module first, then both CLI and GUI (and optionally a REST API) call it.

- [ ] **Choose GUI tech stack** - decide before writing any UI code. Options:
  - **Webapp (recommended):** Python Flask/FastAPI backend + Chart.js/vanilla JS frontend. Low friction, no packaging, runs in browser. Reference: ClaudeApprover for structure - copy and delete that project as the starting point.
  - **Desktop:** Electron, Tauri, or WinForms. Native feel but higher setup cost.
  - Backend approach: Flask serves JSON from AudioMirror XML parsing. Frontend renders charts. OR: AudioManager CLI outputs JSON that the web frontend reads directly.
  - Note: could pull webpages from a localhost service and integrate them, vs building a proper API - weigh up before committing.

- [ ] **API layer - should we build one?** (Optional, decide later but plan for it)
  - If users ever want to integrate AudioManager into other tools, a REST API (e.g., FastAPI endpoints) would be valuable.
  - With architecture from Option 1 above, adding an API is trivial - just another consumer of the core library.
  - **Defer this decision.** Build GUI first; if demand emerges, API can be layered on top without refactoring.
  - Keep architecture flexible so the API layer is "easy to add later" not "blocked by current design."

- [ ] **Decide SpotifyTools generalization** - Should SpotifyPlaylistGen become a shared `SpotifyTools` library used across repos (AudioManager GUI, future tools), or keep repos independent and call the Spotify API directly from each?
  - Modular (independent repos): simpler short-term, duplication risk long-term
  - SpotifyTools shared lib: more upfront work, cleaner if 3+ repos need Spotify
  - Write the decision in HISTORY.md before building any Spotify tab.

---

## TIER G1 - Statistics Dashboard (MVP - Start Here Only)

**Goal: one working tab. Nothing else. No integration, no browsing, no services.**

- [ ] **Framework spike** - before building the full tab, build ONE chart end-to-end to prove the stack. Chart.js is a solid choice for webapp. One chart working = green light to build the rest.

- [ ] **Auto-analysis on startup** - read AudioMirror XMLs on launch, no writes. Fast, non-destructive. Errors shown inline; never blocks the GUI from loading.

- [ ] **Statistics tab panels:**
  - Genre distribution (pie chart)
  - Decade distribution (bar chart)
  - Top artists by track count (horizontal bar chart or sortable table)
  - Library totals: track count, artist count, total size
  - Recent additions (last N tracks by file modification date)
  - Frequency stats (tracks added per month/year - histogram)

---

## TIER G2 - Integration Tab

- [ ] **Integration tab** - GUI wrapper around the CLI integration workflow:
  - Queue of incoming NewMusic tracks with visual decision blocks (similar to CLI Y/N flow)
  - Per-track card display: album art image, artist, album, title, proposed routing destination
  - Routing preview per track (where would it go in the library?)
  - Per-track confirm/decline action buttons (mirrors CLI [Y]/[N] interaction)
  - Live progress indicator during integration run
  - Log viewer showing integration output in-app
  - **Incremental integration:** Process tracks one-by-one or in batches as desired; no requirement to integrate all at once

- [ ] **Built-in integration menu** - trigger TagFixer dry-run, TagFixer real, Integrator dry-run, Integrator real from the GUI. Never requires the user to touch the terminal.

---

## TIER G2b - TagFix Panel

- [ ] **TagFix rules interface** - define and apply metadata correction rules:
  - Rule builder: "if [condition], then [fix]" (e.g., "if genre is empty, set to Unknown"; "if artist ends with '(feat. ...)', extract featured artist")
  - Save rules as presets for reuse
  - Preview: show which tracks match the rule before applying

- [ ] **Batch apply** - apply rules to:
  - Entire library (dry-run first, confirm before executing)
  - NewMusic queue (integrated into Integration tab workflow)
  - Selected tracks (if Library Browser supports multi-select)
  - Log all changes made for undo/review

- [ ] **No individual track editor** - TagFix is NOT mp3tag. Goal is bulk corrections via rules, not manual metadata curation.

---

## TIER G3 - Library Browser

- [ ] **Library tab** - read-only browser over AudioMirror data:
  - Browse: Artist -> Albums -> Tracks
  - Filter by genre, decade, artist prefix
  - Track detail panel (all tags, file path, file size)
  - Full-text search across artist/album/title

---

## TIER G4 - Mirror Tab

- [ ] **Mirror tab** - AudioMirror sync status:
  - Last committed SHA and date
  - Uncommitted changes (new/modified/deleted XML entries)
  - Diff view (what changed since last commit)
  - Manual commit trigger (with dry-run first)

---

## TIER G5 - Services / Cross-Synthesis (Far Future)

- [ ] **Spotify tab** - integrate SpotifyPlaylistGen (or SpotifyTools if that decision is made):
  - Playlist generator from offline library
  - Cross-reference: offline tracks vs Spotify availability
  - Recently played on Spotify

- [ ] **Last.fm tab** - scrobble history and listening stats:
  - Top tracks / artists (weekly, monthly, all-time)
  - Play counts overlaid on Library Browser
  - Listening trends over time (scrobbles per month)

- [ ] **Cross-synthesis view** - overlay all data sources:
  - What do you own offline that's not on Spotify?
  - Tracks with zero Last.fm scrobbles (never listened)
  - Ownership + listening = unified picture of the library

---

## Architecture

**Recommended layered design (from TIER G0 decision):**

```
┌─────────────────────────────────────────────┐
│  User Interfaces                            │
│  ┌────────────────┐  ┌────────────────┐   │
│  │  CLI (scripts, │  │  GUI (webapp)  │   │
│  │   automation)  │  │  (interactive) │   │
│  └────────┬───────┘  └────────┬───────┘   │
└───────────┼────────────────────┼───────────┘
            │                    │
            ▼                    ▼
┌─────────────────────────────────────────────┐
│  (Optional) REST API Layer                  │
│  - added later if needed for integrations   │
└─────────────────┬───────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────┐
│  AudioManager Core Library                  │
│  - routing logic                            │
│  - tagging / metadata correction            │
│  - integration workflow                     │
│  - AudioMirror XML parsing & writing        │
│  - library analysis & stats                 │
└─────────────────────────────────────────────┘
```

**Design principles:**
- Core library contains all business logic. No UI code inside the library.
- CLI and GUI are separate packages that depend on the core library. Both call the same functions - bugs fixed once apply everywhere.
- No duplicate XML parsing, routing logic, or tag-fixing code.
- AudioMirror XMLs are the source of truth. All interfaces read/write through the library.
- GUI is read-only until TIER G2. Integration commands are the first write operations from the GUI.
- AudioManager = the product name. DWave was a working name, dropping it.
- SpotifyPlaylistGen feeds into the Services tab once Spotify integration is stable - either embedded directly or via SpotifyTools.

### Third-party library candidates (for future audio features)

| Library | Purpose | How to get |
|---------|---------|------------|
| TagLibSharp | Read/write ID3, Vorbis, APE, FLAC tags - already in use | NuGet `taglib-sharp` |
| NAudio | Audio playback, waveform analysis, format conversion (C#) | NuGet `NAudio` |
| FFprobe (subprocess) | Extract metadata from any audio format via CLI | `FFprobe.exe` - no C# lib needed |
| AcoustID + MusicBrainz | Acoustic fingerprinting - identify tracks by sound | `acoustid.net` |

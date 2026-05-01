# AudioManager GUI Roadmap

Planning doc for the GUI layer of AudioManager. CLI work lives in IDEAS.md. GUI is a separate development lifecycle - do not start until CLI TIER 1 is stable.

---

## Design Vision

**Sonarr/Radarr-style:** Tabs on the left sidebar, content pane on the right. Clean, functional, data-dense.

**Startup behaviour:** On launch, automatically read AudioMirror XMLs (no regeneration, no writes) and populate all stats panels. Data is always fresh. User opens the app and sees their library immediately.

**Tab order (build in sequence, not in parallel):**

| Tab | Priority | Description |
|-----|----------|-------------|
| Statistics | TIER G1 - start here | Charts, genre/decade distribution, library overview |
| Integration | TIER G2 | NewMusic routing, confirm/decline, run integration from GUI |
| Library Browser | TIER G3 | Browse artists/albums/tracks, search, filter |
| Mirror | TIER G4 | AudioMirror sync status, diff view, commit log |
| Services | TIER G5 (far future) | Spotify, Last.fm, cross-synthesis |

---

## TIER G0 - Decisions (Before Writing Code)

- [ ] **Choose GUI tech stack** - decide before writing any UI code. Options:
  - **Webapp (recommended):** Python Flask/FastAPI backend + Chart.js/vanilla JS frontend. Low friction, no packaging, runs in browser. Reference: ClaudeApprover for structure - copy and delete that project as the starting point.
  - **Desktop:** Electron, Tauri, or WinForms. Native feel but higher setup cost.
  - Backend approach: Flask serves JSON from AudioMirror XML parsing. Frontend renders charts. OR: AudioManager CLI outputs JSON that the web frontend reads directly.
  - Note: could pull webpages from a localhost service and integrate them, vs building a proper API - weigh up before committing.

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
  - Show incoming NewMusic tracks (count and list)
  - Routing preview per track (where would it go?)
  - Per-file confirm/decline (mirrors CLI [Y]/[N] flow)
  - Live progress bar during run
  - Log viewer showing integration output in-app

- [ ] **Built-in integration menu** - trigger TagFixer dry-run, TagFixer real, Integrator dry-run, Integrator real from the GUI. Never requires the user to touch the terminal.

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

- AudioManager CLI is the data layer. GUI reads its outputs (AudioMirror XMLs, reports) or calls CLI commands as subprocesses. No duplicate XML parsing logic.
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

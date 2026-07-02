# Brief for Fable: AudioManager GUI (round 3 - final)

**Everything you need is in this repo and in this document.** You should not need to look outside `C:\Users\David\GitHubRepos\AudioManager`, and you should not need to do further web research - all research decisions (chart types, stack, XML schema, JSON contract, backend architecture) are already made and embedded below. Your job this session is **building, not researching or re-deciding**.

Start here, then read:
- `AudioManager\CLAUDE.md` - build commands, safety rules, file-registration quirks.
- `docs\References\AnalysisJson-Format.md` - **the data contract your whole app consumes** (new this round, see below).
- `docs\Development\GUI-ROADMAP.md` - durable long-term roadmap; this brief is the scoped version of one work session against it.

**Window:** promotional access to Fable 5 ends 2026-07-07 11:59:59 PM PT. Deliberately big, well-scoped, mostly self-contained - chosen to make good use of a scarce budget.

**Visual reference (open before writing any UI code):** `mockup.html` in this folder. Open it in a browser - working tab navigation and live (fake-data) charts across all planned tabs. It went through three rounds of human review. **Implement your GUI's visual language, layout, tab structure, chart selection, and interaction patterns against this mockup.** Don't redesign it from scratch; wire real data and real actions into this shape. You have explicit license to *exceed* its visual polish once structure is locked - see "Go beyond the mockup" at the end.

---

## What changed in round 3 (read this first if you saw an earlier brief)

Round 3 fixed a **critical false assumption** in rounds 1-2 and sharpened the product goal. Four things are materially different:

1. **`analysis --json-output` now really works, and C# owns 100% of data ingestion.** Rounds 1-2 assumed `AudioManager.exe analysis --json-output` emitted library statistics as JSON. **It did not** - the flag was parsed but only wired into `integrate` mode (it wrote *routing* decisions, not stats). Analysis mode silently ignored it and only wrote a markdown report. This has been **fixed at the source**, and the fix now emits **two** JSON files to `logs/`:
   - `logs/analysis-stats.json` (`Code/Doer/Analyser/StatsJson.cs`) - aggregate statistics, reusing the same `StatList` primitives the text report uses so it can't drift from `AudioReport.md`. **This is your Statistics data source.**
   - `logs/tracks.json` (`Code/Doer/Analyser/TracksJson.cs`) - the full per-track array (title/artist/album/year/decade/genres/length/compilation/album-art status + real MP3 `filePath`). **This is your Library Browser data source.**

   **HARD RULE: the Python GUI never reads or parses the raw AudioMirror XML. C# is the only XML parser.** Do not parse `AudioReport.md`, do not reimplement stats in Python, and do not glob/parse AudioMirror XML in Python - consume these two files. This eliminates any second, drifting parser. Full contract: `docs/References/AnalysisJson-Format.md`.

2. **The product goal is now "replace the CLI and BAT scripts entirely."** David's words: *"I want to not use the CLI anymore, not use the BAT scripts anymore - I want this GUI to be my way of using AudioManager from now on."* This raises the bar past "MVP of six tabs." A dedicated **CLI/BAT replacement audit** (below) lists every workflow the GUI must cover, and flags the gaps that need a follow-up C# change to fully close.

3. **Integration is a GUI-native staged workflow, not a terminal.** Rounds 1-2 streamed raw subprocess stdout into a console box. That defeats the point of a GUI - the CLI already prints to a console. Integration is now a **staged review queue** (scan -> per-track visual review with album art + routing preview -> confirm -> structured live progress). A raw log view survives only as a collapsed "Advanced / debug output" affordance, never the primary surface. See the redesigned Integration section and the mockup's Integration tab.

4. **Analysis is demoted from a tab to a Statistics-page control.** Running analysis just regenerates the data the Statistics tab already shows, so a separate "Analysis" surface is redundant. "Re-run analysis" and "Force full regen" now live as a data-freshness control in the Statistics header. Integration keeps its own dedicated left-nav tab (it is a genuinely different function - routing new files, not viewing stats).

---

## What AudioManager is

C# console app (.NET Framework 4.8) that manages David's personal MP3 library (~5,694 tracks at `C:\Users\David\Audio\`). Modes, exposed as CLI args on the built exe (verified in `Code/Program.cs`):

```
AudioManager.exe analysis  [--force-regen] [--json-output] [--no-input] [--no-auto-commit]
AudioManager.exe integrate [--dry-run] [--no-input] [--json-output] [--no-auto-commit]
AudioManager.exe tagfix    [--dry-run]
AudioManager.exe            (no args -> interactive arrow-key menu: Analysis / Analysis Force-Regen / Integrate)
```

The **AudioMirror** is a sibling git repo (`C:\Users\David\GitHubRepos\AudioMirror`, XML under `AUDIO_MIRROR\`) containing one small XML file per track - the per-track data source for the Statistics and Library Browser tabs. Plain data on disk, not a database.

**Critical data-shape fact, drives several design decisions:** David integrates new music in one big batch every 2-4 weeks, not a little each day. Any "recent activity" / "additions over time" view must be batch-shaped (grouped by integration run), never daily-granularity - a daily view is empty almost every day and spikes on integration day, which reads as broken. Documented in `CLAUDE.md` under "David's actual integration cadence."

---

## Backend architecture - DECIDED: subprocess + JSON contract, no daemon

The roadmap's TIER G0 lists three options: (1) extract a shared C# core library both CLI and GUI call, (2) GUI-only, (3) duplicate logic. Round 2 asked whether the exe should instead expose a **long-running local daemon** (HTTP/named-pipe) to avoid reimplementing parsing in Python. **Decision for this build: none of those. Use subprocess invocation + a structured JSON data contract.** Reasoning, so you don't reopen it:

- **The reuse problem is already solved by the JSON contract.** The whole worry was "Python will have to reimplement AudioMirror XML parsing / stats and drift from C# over time." It won't. Statistics data comes from `analysis --json-output` -> `logs/analysis-stats.json`, computed by the C# `Analyser`'s own primitives (guarded by 238 C# tests). Python reads that file. **Zero duplicated stats logic, zero drift** - enforced in code, not by discipline.
- **A daemon buys nothing here and costs plenty.** For a single-user local tool, a daemon means process-lifecycle management (who starts/stops it, crash recovery, port conflicts), a new IPC/API surface to design and version, and more for David to run and debug. The alternative - "run the exe, read a JSON file it wrote" - is simpler, crash-proof (no long-lived state), and already fast enough (a warm-cache `analysis` over 5,694 tracks completes in a few seconds; measured 2026-07-03).
- **The middle-ground question (does Python need to touch XML for the Library Browser?) - no, resolved by design.** Per-track rows come from `tracks.json`, emitted by the same C# analysis run. Python parses **zero** XML. The "shared cache format between C# and Python" the round-2 note wondered about is exactly these two JSON files - C# produces, Python consumes, one parser, no drift. Neither the stats path nor the per-track path needs a daemon or a re-parse per render.
- **This does not box in future tiers.** If a later tier (G5 services, live scrobble overlay) genuinely needs push updates, a daemon/websocket layer can be added *then* as an additive consumer, without touching the stats path. Subprocess-per-invocation is correct for everything G1-G4 does (all user-triggered, none real-time).

**Net:** no core-library extraction this round, no daemon. Shell out to the existing exe; consume its JSON. Document this as the G0 decision in `GUI-ROADMAP.md` ("subprocess + JSON contract chosen; core-library extraction and daemon both explicitly deferred, revisit only if a real-time tier needs it").

**One recommended follow-up C# change (do NOT build this round unless Statistics + Integration preview are done and solid):** to let the Integration tab execute a *per-track* accept/decline selection (rather than all-or-nothing), the exe needs a manifest-driven integrate mode - e.g. `integrate --manifest <accepted.json> --no-input` that moves only the listed files. The current exe integrates everything in NewMusic or nothing. See the Integration section for how the MVP works safely within that limitation, and why closing this gap is the last step to a *complete* CLI replacement. This is the same kind of source change as the JSON fix - flagged, reasoned, but out of scope for this build's core.

---

## Complete CLI/BAT replacement audit (the "no more terminal" goal)

For the GUI to become David's only interface, it must cover every workflow he currently reaches through the CLI or a `.bat`. Here is the full surface and where each piece lands. **Gaps are called out explicitly - do not silently leave them.**

| Current workflow (CLI flag / script) | What it does | GUI home | Status this round |
|---|---|---|---|
| `analysis` (standard) | Incremental mirror refresh + stats + report | Statistics header: **Re-run analysis** | COVERED (reads new JSON) |
| `analysis --force-regen` | Full mirror regen, re-reads cover art from every MP3 (slow, rare) | Statistics header: **Force full regen** (separate control, confirm dialog, progress) | COVERED - build this control, don't hide it |
| `integrate --dry-run` | Preview routing of NewMusic, no file moves | Integration tab: **Scan** stage | COVERED (drives the review queue) |
| `integrate` (real) | Move NewMusic files into library | Integration tab: **Confirm & integrate** | COVERED for all-accepted; **GAP**: per-track selective execution needs the `--manifest` exe mode (see above). MVP = review is a rich preview; execution is accept-all-or-abort |
| interactive `integrate` y/N confirm | Terminal prompt before real integration | Integration tab: **Confirm** step in the queue | COVERED (this is the core "no more terminal" win) |
| `tagfix` | Fixed tag cleanup on NewMusic (hardcoded transforms) | Tag Fix tab | **GAP/skeleton**: the exe's TagFixer is NOT rule-configurable - it applies fixed transforms. The mockup's "rule builder" is aspirational. MVP: rule cards render + "Run Rules" can trigger the exe's existing `tagfix --dry-run`. True configurable rules = future + C# work. Flag this honestly in the tab |
| AudioMirror commit (when exe reports it stale) | `git commit` in AudioMirror before integrating | Mirror tab | **GAP**: Mirror tab is read-only this round. To fully avoid the terminal, it eventually needs a **Commit AudioMirror** action. Note it as the tab's next step |
| `launch.bat` | Build + run interactive menu | Replaced by a GUI launcher (see Distribution) | COVERED - `launch-gui.bat` starts the app, no menu needed |
| `scripts/open_playlist.bat` shortcut | Opens a generated playlist (separate feature) | Not in GUI scope this round | Out of scope - note in roadmap under Services/future |
| `--test` / `--verify` / `--routing-manifest` | Dev/test entry points | N/A | Correctly out of scope (dev-only, not a user workflow) |

**Bottom line for the goal:** after this build, David's normal loop (refresh stats, review + integrate new music, check mirror status) is fully GUI-driven. The two things still needing a terminal or a future C# change are (a) *selective* per-track integration execution and (b) committing AudioMirror and (c) true configurable tag rules. Those are named above so they become the next round's work, not a silent surprise.

---

## Scope this session: all tabs, priority-ordered

David wants the whole app shape, and is comfortable with skeleton later-tiers as long as Statistics and Integration are functionally real. Build in this order; stop moving down once budget runs low. A fully-working Statistics + Integration with skeleton Library/TagFix/Mirror/Services is a good outcome - don't sacrifice the first two's quality to rush the rest.

**Gate between tabs on a real check:** before moving on, load the tab in the browser and confirm its data renders correctly against real data (not just "it compiles"). If Statistics hits a data/perf problem, fix it before starting Library Browser.

1. **Statistics** (real, full-featured - the centerpiece; includes the Analysis re-run controls)
2. **Integration** (real - staged review workflow driven by the exe's dry-run JSON; see its section)
3. **Library Browser** (MVP - browse/search/filter over AudioMirror XML, rich columns, table/grid, pagination)
4. **Tag Fix** (skeleton - rule cards render; "Run Rules" may trigger `tagfix --dry-run`, no configurable rules yet)
5. **Mirror** (skeleton - read-only status from `git log`/`git status` against AudioMirror)
6. **Services** (placeholder - two stretch stub cards; do not build)

### Build one data loader first, before any UI code

Statistics, Library Browser, and the batch panels read from the **two C#-emitted JSON files** - never from XML. Write a single module (`gui/data_loader.py`) with explicit, individually testable accessors that owns both:

- **Stats:** runs `AudioManager.exe analysis --json-output --no-input` once at startup (and on manual refresh) - this single run produces *both* JSON files - then reads `logs/analysis-stats.json`. Every stat tile and Statistics panel maps 1:1 to a field in that file (see `AnalysisJson-Format.md`). **Read `schemaVersion` and fail loudly on a mismatch** rather than mis-reading.
- **Per-track rows:** reads `logs/tracks.json` once into an in-memory index, exposes typed accessors (page/slice/filter/search). Don't re-read the file per render. **Do NOT glob or parse AudioMirror XML in Python - `tracks.json` is the only per-track source.** Album art is extracted lazily from each row's `filePath` via `mutagen` (that's reading an MP3's embedded image, not parsing XML data) and cached - see the Library Browser section.
- **Batch history:** owns the `stats-history.json` batch-delta cache (below) and a `refresh()` method.

Every panel and the Library Browser consume this loader; none touch the exe output paths or the XML directly. **The GUI parses no XML at all** - this is the single highest-leverage structural decision in the brief, and it is now enforced by the data contract, not by discipline.

**Prove the loader end-to-end on one panel first:** get Genre Distribution (with its donut/pie/treemap swap) fully working against `analysis-stats.json` and committed, then move on. Catches loader + chart-library friction once, cheaply.

### Safety constraints (read `CLAUDE.md` for the full list)

- The music library (`C:\Users\David\Audio\`) and NewMusic inbox are **not backed up**. **Never write to them, or to AudioMirror, from GUI/Python code.** The only writes that happen are through the exe's own modes, which have their own safety checks (dry-run, confirm flow, pre-integration gate). Your GUI triggers the exe; it never reimplements or bypasses those paths. This specifically means the Integration review queue must not physically move or delete files in NewMusic itself (see its section).
- Read-only `git log`/`git status` against AudioMirror for the Mirror tab is fine.
- If unsure whether an operation is read-only/safe, don't do it - note it in your summary instead of guessing.

---

## Statistics tab: full chart/panel spec (build against mockup.html + the JSON contract)

Every panel maps to a field in `analysis-stats.json`. Build all of them; the "stretch" list is optional.

### Data-freshness control (new this round - the demoted Analysis function)

In the Statistics page header, next to the global date-range selector, show a **data-freshness card/control**:
- "Analysis last run: <relative time from `generatedAt`>"
- **Re-run analysis** button -> runs `analysis --json-output --no-input`, shows a small inline progress state, re-reads the JSON, re-renders. This is fast (seconds).
- **Force full regen** button (secondary, with a confirm dialog) -> runs `analysis --force-regen --json-output --no-input`. This is the slow path (re-reads cover art from every MP3) - show a proper progress indicator and disable it while running. Use `--no-auto-commit` semantics carefully: force-regen may change AudioMirror XML; surface that in the Mirror tab rather than auto-committing from the GUI.

This is the entirety of the "Analysis tab" - a control, not a page. Justify it in the UI copy: analysis output *is* the Statistics data.

### Stat tile row (top) - all from `summary` in the JSON

| Tile | JSON field | Notes |
|---|---|---|
| Tracks | `summary.trackCount` | show "+N vs last batch" delta (see batch delta below) |
| Artists | `summary.artistCount` | |
| Total Size | `summary.totalLibraryBytes` | **now a real on-disk sum** (the exe walks the library once; no Python proxy needed) |
| Genres | `summary.genreCount` | |
| Total Playback | `summary.totalPlaybackHours` | format "360.9 hrs" |
| Avg Song Length | `summary.avgSongLengthSeconds` | format m:ss |
| Median Song Length | `summary.medianSongLengthSeconds` | format m:ss |
| Avg File Size | `summary.avgFileBytes` | format MB |

**File-size note, now resolved:** rounds 1-2 told you to fake file size from bitrate because per-track disk stats in Python would be slow. That workaround is gone - `totalLibraryBytes`/`avgFileBytes` are computed by the exe in a single disk walk it was doing anyway, and arrive in the JSON for free. Use them. (Per-track exact size in the Library Browser's optional "Size" column still has no cheap source - keep that column out, as before.)

**Batch delta definition:** compare the current snapshot to the snapshot at the previous integration batch. Store each analysis run's `summary` (tiny), keyed by the git-derived batch it corresponds to, in `gui/.cache/stats-history.json` (GUI-owned; NOT AudioMirror, NOT the library), and diff against the second-most-recent batch entry. This grows by roughly one small row per batch (a few dozen a year) - no scale concern over years.

### Panels (each -> a JSON field)

- **Genre Distribution** - donut/pie/treemap swappable (pattern in mockup). Source: `genreDistribution` (already per-genre counted; a multi-genre track appears in each genre, matching the report).
- **Decade Distribution** - bar/donut swappable. Source: `decadeDistribution`.
- **Year Distribution** - horizontal bar, top years. Source: `yearDistribution` (sorted desc; show top N with a "show all").
- **Genre Balance (radar)** - same `genreDistribution` data, radar read. **Chart-theming fix, mandatory and systemic:** ApexCharts (or your chart lib) does not inherit page CSS variables for axis/legend/tooltip text - set them explicitly on **every** chart (`yaxis/xaxis.labels.style.colors`, `legend.labels.colors`, `tooltip.theme:'dark'`). Round 1's radar shipped white-on-white. Apply to all panels, not just the radar.
- **Top Artists** - horizontal bar, toggle "Excl. Musivation" / "All". Source: `topArtists.exclMusivation` / `topArtists.all` (Akira The Don dominates "all" via the Musivation collection and is absent from "excl" - both are in the JSON, top 50 each).
- **Recent Additions** - grouped by integration batch, header "Batch <date> (<N> tracks)". **Batch boundaries: use AudioMirror commit history as the single canonical source everywhere a "batch" appears** (Recent Additions, delta tiles, batch bar chart) - it predates the GUI cache and covers every past run. `stats-history.json` stores stat *values* per batch keyed to those same git boundaries, not an independent batch definition.
- **Tracks Added Per Integration Batch** - bar, one per batch, last 6-10 batches. Replaces round-1's daily heatmap (wrong shape for batch data). Same boundaries as Recent Additions.
- **Track Age Distribution** - bar, buckets `0-2y / 2-5y / 5-10y / 10-20y / 20y+`. Source: `ageDistribution` (fixed 5 buckets, always present). Pair with the scalar `ageStats` (average/median/newest/oldest years) as a small callout.
- **Cover Art Resolution Breakdown** - bar of `coverArt.dimensionHistogram` (top 15 "WxH" buckets).
- **Tag Completeness ring** - radial widget. Source: `tagCompleteness.percent` (% of tracks with Title/Artist/Album/Genre/Year all present). Reusable component - build once, use for both rings.
- **High-Res Cover Coverage ring** - same widget. Source: `coverCoverage800.percent` (% of all tracks with cover >= 800px on the short side; this is `covered/total`, honestly counting no-cover tracks as not-covered).
- **Global date-range filter** - dropdown (All time / current year / previous year / last batch) in the header; re-renders the time-windowed panels (Year/Decade/Age/batch - not Genre/Cover, which aren't time-windowed). Mockup demos this on the Year chart; extend consistently.

### Configurability pattern (generalize)

Genre (donut/pie/treemap), Decade (bar/donut), Top Artists (Excl./All toggle), plus the global date filter. New panels beyond these should prefer a similar toggle/swap over a fixed single view - cheap once the pattern exists.

### Stretch, if budget allows

Artist treemap (genre -> artist share); library-size-vs-track-count dual-axis over batches; year-over-year toggle; top-10-vs-long-tail split.

---

## AudioMirror XML schema (C#'s input - the GUI never reads this)

**This section is reference for understanding data lineage, not an instruction to parse anything.** The GUI does not open these files - `tracks.json` (per-track) and `analysis-stats.json` (aggregate) are derived from them by C# and are your only sources. Shown here so you understand where the JSON fields come from.

Location: `C:\Users\David\GitHubRepos\AudioMirror\AUDIO_MIRROR\` - one `.xml` per track in subfolders (`Artists/<Artist>/<Album>/`, plus `Compilations/`, `Miscellaneous Songs/`, `Motivation/`, `Musivation/`, `Sources/`). The C# `Parser` recursively globs `*.xml`; Python does not.

```xml
<?xml version="1.0" encoding="utf-8"?>
<Track>
  <Title>see the real</Title>
  <Artists>21 Savage</Artists>          <!-- semicolon-separated if multiple, first = primary -->
  <Album>american dream</Album>
  <Year>2024</Year>
  <TrackNumber>10</TrackNumber>
  <Genres>Rap; Hip Hop</Genres>          <!-- "; " separated -->
  <Length>00:03:02.6250000</Length>      <!-- .NET TimeSpan, hh:mm:ss.fffffff -->
  <AlbumCover><Count>1</Count><Width>1200</Width><Height>1200</Height></AlbumCover>
  <Compilation>True</Compilation>        <!-- "True" / "False" string -->
</Track>
```

Strict schema (locked 2026-06-06) - all fields present. Full reference: `docs/References/AudioMirror-Format.md`. Every field above surfaces in `tracks.json` (per track) and/or `analysis-stats.json` (aggregated), computed by C#. **You consume those JSON files; you never parse this XML.**

---

## Library Browser tab: columns, views, and REAL album art

Build against the mockup's richer version. **All rows come from `tracks.json`** (via `data_loader`) - every column below is already a field in that file; Python computes nothing from XML.

**Default visible columns:** Title (`title`), Artist (`primaryArtist`/`artists`), Album (`album`), Genre (`primaryGenre`), Year (`year`), Track Number (`trackNumber`), Length (`length`, already m:ss).

**Optional columns (iTunes-style show/hide picker, off by default):** Date Added (`addedDate`), Compilation (`compilation` boolean), Cover Thumbnail (see album art below).

**Do not add:** Play Count, Rating, BPM - no backing field in AudioMirror.

**Album art - this is the point of the grid view, get it right:**
- **Grid view (Sonarr-poster style)** is the headline: each track is a card whose image is the track's **real album cover art**, extracted with `mutagen` (pure-Python, reads embedded APIC/cover frames directly from the MP3). Use the row's **`filePath`** (already the real MP3 path, provided by C# in `tracks.json`), read the embedded picture, render it as the card image. This is reading an image out of an MP3 - it is NOT parsing XML data, so it does not violate the no-XML rule. Cache extracted thumbnails (`gui/.cache/thumbs/`, keyed by `id`) so you extract once, not per render.
- Use the row's **`hasArt`/`hiResArt`** flags to skip extraction attempts on tracks with no art and to badge low-res covers - no need to open the file to know its art status.
- The grid must **read as an album-art wall**, not a placeholder field. The mockup shows varied per-card placeholder covers (distinct colors + album initials) specifically so the *concept* is legible with fake data - **build toward real extracted art as the target, not "the placeholder is fine."** A colored placeholder is an acceptable fallback only for a track whose art can't be read (or whose `filePath` doesn't resolve - rare, see the contract doc).
- Card also has a thin status bar (color by `primaryGenre`) and an "Added: <addedDate>" line.
- **Table view** Cover Thumbnail column uses the same extracted-thumbnail source at small size.

**Search + filters:** full-text across Title/Artist/Album; filter chips for genre/decade/artist (as in mockup).

**Pagination:** 5,694 tracks needs real server-side pagination - only render the current page's rows (slice the loader's in-memory index for the current page + filters). Don't ship all rows and hide them client-side. Port the mockup's pagination row (page numbers + jump-to-position). Thumbnail extraction is per-visible-page only (never extract 5,694 covers up front).

---

## Integration tab: GUI-native staged workflow (NOT a terminal)

**Design principle:** the CLI already prints to a console. A GUI that just mirrors that stream adds nothing. Integration is a **staged, visual workflow**. A raw log exists only as a collapsible "Advanced / debug output" panel, defaulted closed - never the primary surface.

The exe's real integration is all-or-nothing (see the architecture section's `--manifest` note), so the MVP is honest about what "accept/decline" can execute. Stages:

**Stage 1 - Scan.** A **Scan NewMusic** button runs `integrate --dry-run --no-input --json-output` as a subprocess. This produces `logs/routing-<timestamp>.json` - the existing routing contract (array of `{filename, artist, title, album, destination, reason, isNewFolder, status, inBatchDuplicate, tagChanges[]}`; schema in `MusicIntegrator.WriteJsonOutput`). Parse that file, not stdout. Show an inline progress state while it runs (dry-run over NewMusic is quick).

**Stage 2 - Review queue.** Render one **card per proposed track** from the routing JSON:
- Album art (extract via `mutagen` from the NewMusic file), title, artist, album.
- Proposed **destination path** and the **routing reason** (both already in the JSON).
- **Tag changes** as before -> after chips (from `tagChanges[]`).
- Status badge: New Folder (`isNewFolder`), In-Batch Duplicate (`inBatchDuplicate`), or a `status` value.
- Per-card **Accept / Decline** toggle and a bulk select-all / accept-all.
- Filter chips: "Only conflicts/duplicates", "Only new folders", "All".

This is a genuine visual review of exactly what the real run will do - the data is real, per-track, with art and routing rationale. It replaces reading a wall of dry-run text.

**Stage 3 - Confirm.** A summary bar: "N tracks -> M artists, K new folders, D duplicates/conflicts; X declined". A single **Integrate accepted tracks** primary button, enabled only after a scan.

**Stage 4 - Execute + structured progress.** On confirm, run the real integration and show **structured progress** - an overall progress bar plus a per-track status list transitioning queued -> moving -> done / failed, driven by parsing the exe's line-by-line output into state (read the pipe unbuffered/line-by-line; do NOT wait for exit). NOT a raw text dump. On completion, a result summary (moved / skipped / errors) and a link to the run log.

**The honest MVP limitation (surface it in the UI, don't hide it):** because the exe integrates all-of-NewMusic or nothing, the MVP's real execution is **accept-all** (Stage 4 runs `integrate --no-input`). If David declines specific tracks, the correct MVP behavior is to **not execute** and tell him "declining individual tracks needs manual removal from NewMusic first" - the GUI must NOT move/delete NewMusic files itself (safety rule). True per-track selective execution is unlocked by the recommended `integrate --manifest` exe mode (architecture section) - build the review queue now so it's ready to drive that mode later; wire Stage 4 to accept-all for this round.

**Subprocess discipline (mandatory on every exe call, all tabs):**
- **Always pass `--no-input`** (including dry-run) - the exe has an interactive confirm path; a subprocess with no stdin will hang forever if it hits a prompt.
- **Capture stdout AND stderr** (errors only on stderr would vanish silently). Read line-by-line, unbuffered.
- **One exe invocation at a time.** Disable trigger buttons while a subprocess runs (prevents two concurrent analysis/integrate runs racing on AudioMirror regen). See Failure Modes for concurrency.
- Smoke-test the streaming against a real long run (`analysis --force-regen`) before considering this done.

---

## Failure modes, error handling, and the Subprocess Error Modal

The brief must specify what happens when a triggered subprocess misbehaves. It touches real filesystem-adjacent operations (via the exe), so this is not optional polish.

**Exit codes the exe actually uses** (from `Program.cs`): `0` success; `1` gate/validation failure or unknown mode (e.g. pre-integration gate: AudioMirror stale or LibChecker dirty); `123` unhandled exception (the exe prints a `Message:` and `Stack Trace:` block before exiting). Map these to human messages; treat any non-zero as failure.

**Hang / timeout.** Every subprocess call has a **timeout** and a visible **Cancel** button that kills the process tree. Because `--no-input` is always passed, hangs shouldn't come from prompts, but a genuinely stuck run (e.g. disk stall) must be killable without closing the app. Default timeouts: dry-run/analysis a few minutes; force-regen longer (it re-reads every MP3's art) - make it generous and progress-driven, not a hard short kill.

**Subprocess Error Modal - exact layout (build this component, reuse it for every exe call):**
```
+-------------------------------------------------------------+
|  [x]  Analysis failed                                        |   <- title = "<action> failed"
|-------------------------------------------------------------|
|  Exit code: 1  -  Pre-integration gate failed:              |   <- code + interpreted meaning
|  AudioMirror is out of sync with the library.               |      (parsed from known exit codes /
|                                                             |       the exe's first ERROR line)
|-------------------------------------------------------------|
|  Details                                    [Copy] [v/^]    |   <- collapsible; Copy = full command
|  > AudioManager.exe integrate --dry-run --no-input ...       |      + exit code + full captured output
|  > Pre-integration validation...                            |      to clipboard
|  >  - ERROR: AudioMirror is out of sync ...                 |   <- monospace, scrollable tail of
|  >  (for exit 123: the parsed "Stack Trace:" block)         |      stdout+stderr
|-------------------------------------------------------------|
|                          [ Dismiss ]   [ Retry execution ]  |   <- Retry re-runs the SAME command
+-------------------------------------------------------------+
```
- **Copy-to-clipboard** copies the exact command line, exit code, and full captured stdout+stderr - so David can paste it to you or into an issue without retyping.
- **Retry execution** re-invokes the identical command (same args, same working dir) - a one-click retry loop for transient failures.
- For exit `123`, parse the exe's `Stack Trace:` section out of the captured output and show it in Details.
- For exit `1` gate failures, lift the exe's first `- ERROR:` line into the interpreted-meaning slot (e.g. "AudioMirror is out of sync", "LibChecker found issues") so David sees the actionable cause without reading the log.

**Concurrency / data drift.**
- **GUI-internal:** serialize all exe calls (one at a time, buttons disabled while running) so two GUI-triggered analysis/integrate runs never race on AudioMirror regeneration.
- **GUI vs a terminal David opens anyway:** if he runs a CLI command while the GUI is mid-operation, both could regenerate AudioMirror. MVP mitigation: document "finish GUI operations before running CLI commands"; the exe's own pre-integration gate already refuses to integrate against a stale/dirty mirror, so the dangerous path (integrate) is self-protecting. A GUI file-lock is a stretch goal.
- **AudioMirror vs disk drift:** `analysis` regenerates the mirror from disk, so a refresh re-syncs. The Mirror tab surfaces uncommitted count so drift is visible. If the mirror is stale at integration time, the exe's gate blocks and the Error Modal shows why.

---

## Testing / verification strategy (was missing entirely - add it)

The C# side is covered (238 tests incl. the new `StatsJson` contract). The GUI's own burden is bounded because it never performs file operations itself - it triggers the exe (which owns those, with its own tests and safety gates). So the GUI test scope is **data parsing + rendering correctness + not-crashing on bad data**:

- **`data_loader` unit tests** against a small committed fixture (a handful of AudioMirror XML files + a canned `analysis-stats.json`): field mapping, pagination/slice/filter correctness, empty-library empty states, and a **schemaVersion-mismatch** case that must fail loudly.
- **Contract smoke test:** a test that runs `analysis --json-output` once against the real (or a fixture) library and asserts every field `data_loader` reads is present in the produced JSON - catches C#/Python schema drift the moment it happens.
- **Subprocess-flow manual smoke checklist** (file moves can't be safely unit-tested): scan -> review -> (dry-run) confirm renders; error modal appears on a forced non-zero exit; cancel kills a long run. Document the checklist in `gui/README.md`.

Keep it proportional - this is a personal single-user tool, not a service. The point is that the *data path* is tested and the *destructive path* stays inside the already-tested exe.

---

## Tag Fix tab (skeleton - be honest about what it is)

Build the rule-card UI from the mockup (Maintainerr pattern: name, description, status badge, scope metadata, Preview/Edit/Delete, top-level New Rule / Run Rules). **Functional stub:** cards render and are editable in the UI; "Preview Matches" can show a computed-or-hardcoded count.

**Honesty flag (put a short note in the tab):** the exe's `TagFixer` applies **fixed, hardcoded** transforms (TCMP, genres, parentheticals, featured-artist extraction) to NewMusic - it is not rule-configurable. So the mockup's "rule builder" describes a *future* capability, not what the exe does today. For this round, "Run Rules" may at most trigger `tagfix --dry-run` (the exe's existing fixed behavior) and show its output; it must not claim to run user-defined rules. True configurable rules need a C# change and are future work.

---

## Mirror tab (skeleton)

Read-only status: last commit SHA/date, uncommitted change count, via `git log -1` / `git status` against the AudioMirror repo path. **Read-only - never write to AudioMirror.** Note in the tab that a **Commit AudioMirror** action is the planned next step (needed to fully remove the terminal from David's loop - see the CLI-replacement audit).

---

## Services tab (stretch, not required)

Two feasibility stub cards, both optional and gated behind finishing the required tabs:
- **Last.fm read-only** - lowest-effort real integration. `user.getRecentTracks` etc. work with a free API key, **no OAuth**. If budget remains after the core tabs, a minimal real integration (recent scrobbles, or a "never scrobbled" cross-reference) is a genuine extra.
- **Spotify read-only** - higher effort. Authorization Code + PKCE, an app registered in the Spotify dashboard (David does this himself), a `localhost` redirect. No client-secret risk. Only after Last.fm, only if clear budget.

Keep TIER G5 "far future" in the roadmap - this is an invitation to go further, not a scope change.

---

## Distribution / launch (how this replaces `launch.bat` operationally)

The "no more BAT scripts" goal includes *starting* the app. Provide a **`launch-gui.bat`** (double-click) that starts the GUI and opens it, with **no lingering console window**:
- If NiceGUI (recommended): run in native/desktop-window mode, or start the server with `pythonw`/minimized and open the browser to it. The user should get a window, not a terminal.
- Add a note in `gui/README.md` on making a Start-Menu/desktop shortcut to `launch-gui.bat` so David launches from an icon, matching how `launch.bat` was reached.
- It would be self-defeating (see Integration feedback) to require a visible terminal to run a GUI whose whole point is escaping the terminal - make the launcher windowless.

---

## Chart library and stack decision (already researched - build, don't re-research)

**Stack: your call, same as prior rounds:**
- **NiceGUI** (Python-native, FastAPI + Vue/Quasar, WebSocket live updates, `ui.echart`/`ui.plotly`) - **recommended.** Pure-Python UI, no JS build step, easiest to wire subprocess calls and file reads into event handlers. `ui.echart` (Apache ECharts) has every chart type here (radar, bar, donut/pie, treemap, gauge). Native-window mode also solves the windowless-launch requirement above.
- **Flask + ApexCharts/Chart.js** - safe fallback. The mockup's ApexCharts is directly portable (mockup uses ApexCharts via CDN as a stack-neutral reference).

**Chart-type choices (don't re-derive):** donut/pie/treemap for Genre (three reads on proportional share); vertical bar for Decade/Year/batch/age/cover-resolution (least-ambiguous categorical counts); horizontal bar for Top Artists and Year (long labels read better unrotated); radar for Genre Balance (deliberate second read); radialBar/ring for the two coverage percentages. Explicitly rejected: calendar/scrobble heatmap (wrong shape for batch data), sankey (no source-to-destination flow in this data).

---

## Where to put the code

New top-level `AudioManager\gui\` folder. Keep it fully separate from `project\AudioManager\` (the C# solution) so you don't risk the CLI build. **Do NOT read, modify, or re-register anything in `project\AudioManager\Code\` or the `.csproj`** - the exe's CLI surface (documented above) plus the two JSON contracts are your only integration points, and the C# solution is otherwise out of scope. (The one C# change this build depends on - `analysis --json-output` - is already done and committed on branch `feat/analysis-json-output`; you consume it, you don't touch it.)

**Commit frequently** - one commit per tab/panel completed, not one at the end. See `contingency-plan.md` for why (usage pauses preserve session state; only committed work survives a hard kill).

---

## Go beyond the mockup

The mockup is the **structural floor** - tab layout, dark theme, panel grid, chart selection, interaction patterns (column picker, view toggle, rule cards, radial rings, the staged Integration workflow). It is **not** the polish ceiling. Once the structure is locked and the required tabs are functionally real, exceed the mockup's visual richness - spacing, micro-interactions, loading/empty states, chart-transition animation, real album-art walls. Don't spend this license before the structural requirements are met.

The `frontend-design` plugin skill is enabled - use it **only** for polish (typography, spacing/weight, background depth). Do **not** let it override the approved dark theme, tab structure, chart-library choice, accent system, or the JSON/subprocess architecture - those are locked.

---

## Definition of done

- GUI loads and shows all tabs; **Statistics and Integration are fully functional against real data / real subprocess calls**; others may be skeleton per the priority order.
- **Statistics** reads exclusively from `logs/analysis-stats.json` (via `data_loader`), includes every panel in the spec with configurability (chart swaps, artist toggle, global date filter) and explicit chart theming on every chart, and includes the **Re-run analysis** + **Force full regen** freshness controls (the demoted Analysis function). Every `analysis-stats.json` category has a visible home.
- **Integration** is the staged **scan -> review-queue -> confirm -> structured-progress** workflow driven by the exe's dry-run routing JSON, with real per-track album art and routing reasons - **not** a raw terminal stream (raw log is a collapsed advanced affordance only). Real execution is accept-all this round, with the per-track-selective limitation surfaced honestly in the UI.
- **Analysis and Integration are separated**: Integration is its own tab; Analysis is a Statistics-page control, not a tab.
- **Library Browser** reads all rows from `logs/tracks.json` (never XML), has the full column set (+ picker), table/grid toggle with **real `mutagen`-extracted album art** in the grid (from each row's `filePath`; placeholder only as fallback), server-side pagination, and per-visible-page thumbnail extraction.
- **Decoupling verified:** the GUI/Python parses no AudioMirror XML anywhere - all data comes from `analysis-stats.json` and `tracks.json`. A `grep` of the `gui/` folder for XML parsing should come up empty (mutagen image reads and `git`/subprocess calls excepted).
- **CLI/BAT replacement:** the audit table's COVERED rows work end-to-end from the GUI; the GAP rows (per-track selective integrate, AudioMirror commit, configurable tag rules) are surfaced in-UI as next steps, not silently omitted.
- **Failure handling:** the Subprocess Error Modal (with copy-to-clipboard and retry) is implemented and reused for every exe call; subprocess calls are serialized, cancellable, and always pass `--no-input`.
- **Launch:** a windowless `launch-gui.bat` starts the app; `gui/README.md` explains shortcut setup.
- **Tests:** `data_loader` unit tests (incl. schemaVersion mismatch + empty-library) and the contract smoke test pass; the subprocess manual smoke checklist is documented.
- **No writes anywhere except through the exe's own write paths.** GUI/Python code never writes to the library, NewMusic, or AudioMirror.
- `GUI-ROADMAP.md` updated: G0 decisions (stack; subprocess+JSON; daemon and core-library both deferred) checked off with reasoning; the real-file-size resolution noted; G1+ items checked off for what you built.
- **Report back:** stack chosen and why; which tabs got full vs skeleton; how far the Integration per-track limitation was taken; Services stretch attempted or not; anything deferred.

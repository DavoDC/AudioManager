# Brief for Fable: AudioManager GUI (round 2 - final)

**Everything you need is in this repo and in this document.** You should not need to look outside `C:\Users\David\GitHubRepos\AudioManager`, and you should not need to do further web research - all research decisions (chart types, stack, inspiration takeaways, Services feasibility) are already made and embedded below. Your job this session is **building, not researching**.

Start here, then read `AudioManager\CLAUDE.md` (build commands, safety rules, file registration quirks) and `docs\Development\GUI-ROADMAP.md` (full multi-session roadmap - this brief is the scoped, self-contained version of one work session against it).

**Window:** promotional access to Fable 5 ends 2026-07-07 11:59:59 PM PT. This is a deliberately big, well-scoped, mostly self-contained task chosen to make good use of a scarce budget.

**Visual reference (read/open before writing any UI code):** `mockup.html` in this same folder. Open it in a browser - it has working tab navigation and live (fake-data) charts across all six planned tabs. This is the **round-2, David-approved** mockup - it went through two rounds of human review (`PRIVATE_NOTES/memory/processes/mockup-before-fable-build.md`, workspace-internal process doc, not needed here). **Implement your GUI's visual language, layout, tab structure, chart selection, and interaction patterns (column picker, view toggle, rule cards, radial rings, comparison deltas) against this mockup.** Don't redesign it from scratch; wire real data and real actions into this shape. You have explicit license to *exceed* its visual polish once the structure is locked in - see "Go beyond the mockup" at the end of this brief.

---

## What AudioManager is

C# console app (.NET Framework 4.8) that manages David's personal MP3 library (~5,693 tracks at `C:\Users\David\Audio\`). It has two modes, **already exposed via CLI args on the built exe** (verified in `Code\Program.cs`):

```
AudioManager.exe analysis [--force-regen] [--json-output]
AudioManager.exe integrate [--dry-run] [--no-input] [--json-output] [--no-auto-commit]
```

The **AudioMirror** is a sibling git repo (`C:\Users\David\GitHubRepos\AudioMirror`) containing one small XML file per track - the data source for the Statistics and Library Browser tabs. Plain data on disk, not a database.

**Critical data-shape fact, drives several design decisions below:** David integrates new music in one big batch every 2-4 weeks, not a little each day. Any "recent activity" or "additions over time" view must be batch-shaped (grouped by integration run/week), never daily-granularity - a daily view is empty almost every day and spikes hard on integration day, which reads as broken. This is documented in `AudioManager\CLAUDE.md` under "David's actual integration cadence."

---

## Scope this session: MVP versions of ALL six tabs

David wants to see the whole app shape this round, not just one tab, and is comfortable with simple/skeleton versions of the later tiers as long as Statistics and Integration are functionally real. Build in this priority order, and stop moving down the list once you're running low on session budget - a fully-working Statistics + Integration with skeleton Library/TagFix/Mirror/Services is a good outcome; don't sacrifice Statistics/Integration quality to rush the rest.

**Gate between tabs on a real check, not a feeling:** before moving from one tab to the next, load it in the browser and confirm its data actually renders correctly against real data (not just "the code compiles"). If Statistics hits a data or performance problem, fix that before starting Library Browser - don't rush ahead to "show the whole shape" while leaving Statistics half-working, since that violates the priority order above (Statistics/Integration quality over breadth).

1. **Statistics** (real, full-featured - this is the centerpiece, see chart requirements below)
2. **Integration** (real - trigger the exe's existing `analysis` / `integrate --dry-run` / `integrate --no-input` modes as subprocesses, stream output into a console panel that fills available vertical space, not a cramped fixed box. **Always pass `--no-input` on subprocess-triggered runs** - the exe supports an interactive confirm flow, and a subprocess with no stdin attached will hang the GUI indefinitely if it ever hits a prompt.)
3. **Library Browser** (MVP - browse/search/filter over the same AudioMirror data already loaded for Statistics, with the richer column set and view toggle described below)
4. **Tag Fix** (skeleton acceptable - rule-card UI per the mockup, "Run Rules" does not need to execute real tag changes yet, but the cards should render as a real feature-in-progress, not empty boxes)
5. **Mirror** (skeleton acceptable - read-only status display: last commit SHA/date, uncommitted count, from `git log`/`git status` run against the AudioMirror repo path, read-only)
6. **Services** (placeholder tab with two stretch-goal stub cards - see "Services tab" section below, do not build required functionality)

### Build one data loader first, before any UI code

Statistics, Library Browser, and the Recent Additions/batch panels all read from the same two sources: `analysis --json-output` and the AudioMirror XML files. Write a single module (e.g. `gui/data_loader.py`) with explicit, individually testable accessor functions (e.g. `get_stats()`, `get_tracks_for_batch(batch_id)`, `get_genre_distribution()`) that owns both:

- Runs `analysis --json-output` once at startup (and on manual refresh). Check the exe's actual JSON field names as you write the loader, and note them inline as comments or in `gui/README.md` as you go - this is a normal "check the data before consuming it" step, not a stopping point; keep building. Fall back to AudioReport.md text parsing only for fields genuinely missing from JSON, and prefer recomputing from the AudioMirror XML over regex-parsing report text wherever the XML has the same data (XML is structured and stable; the text report is a rendering of it and more brittle to parse).
- Globs and parses the AudioMirror XML once at startup, builds an in-memory (or `gui/.cache/mirror-index.json`) index, and exposes typed accessors - don't re-glob or re-parse 5,693 XML files on every panel render or every page load. Cold load shouldn't feel sluggish to a human watching it start; if your first pass is slow, cache is the fix, not a specific library choice.
- Owns the `stats-history.json` batch-delta cache described below and exposes a `refresh()` method.

Every panel, tile, and the Library Browser consume this loader; none of them touch the exe, the JSON, or the XML files directly. This is the single highest-leverage structural decision in this brief - it's the shared root of the JSON-schema risk, the XML-performance risk, and the batch-delta risk below, and fixing it once here means you don't re-solve it per panel.

**Before building the rest of Statistics, prove the loader end-to-end on one panel first:** get Genre Distribution (with its donut/pie/treemap swap) fully working against real data and committed, then move on. This catches loader bugs and chart-library friction (theming, swap mechanics) once, cheaply, instead of after all ten panels are wired up.

### Why subprocess, not core-library extraction

The roadmap doc's TIER G0 lists "extract a shared core C#/GUI library" as a recommended architecture decision. For this session, **don't do that refactor.** The exe already accepts the args above and already has `--json-output` support - shelling out to the existing binary and parsing its output is lower-risk, zero-change-to-CLI-code, and gets you working Integration today. Document this as the G0 decision for now in `GUI-ROADMAP.md` (check off the item, note "subprocess invocation chosen for MVP; revisit core-library extraction only if a future tier needs deeper C# logic access than the exe's CLI surface provides").

### Safety constraints (read `CLAUDE.md` for the full list)

- The music library (`C:\Users\David\Audio\`) and NewMusic inbox are **not backed up**. Never write to them directly from GUI code - the only writes that happen are through the existing exe's own integrate mode, which already has its own safety checks (dry-run, confirm flow). Your GUI code triggers the exe; it doesn't reimplement its logic.
- Never write to the AudioMirror repo directly either, except read-only `git log`/`git status` for the Mirror tab.
- If unsure whether an operation is read-only or safe, don't do it - note it in your summary instead of guessing.

---

## Statistics tab: full chart/panel spec (build against mockup.html)

The mockup's Statistics tab is the structural target. Every panel in it maps to a real data source below. Build all of them; the "stretch" list at the end is optional if time allows.

### Stat tile row (top)

8 tiles, all sourced from `analysis --json-output` (or the equivalent AudioReport.md fields if JSON output doesn't cover a stat - check the exe's actual JSON schema first, fall back to parsing the text report only if a field is genuinely missing from JSON):

| Tile | Source | Notes |
|---|---|---|
| Tracks | track count | show a "+N vs last batch" delta comparing current count to the count at the previous integration run (see "batch delta" note below) |
| Artists | distinct artist count | |
| Total Size | sum of resolved MP3 file sizes | see file-size note below |
| Genres | distinct genre count | |
| Total Playback | sum of all track lengths, formatted as "360.9 hrs" | AudioReport.md "Total playback hours" |
| Avg Song Length | mean track length | AudioReport.md "Average song length" |
| Median Song Length | median track length | AudioReport.md "Median (typical) song length" |
| Avg File Size | mean resolved file size | AudioReport.md "Average file size" |

**Batch delta definition:** compare the current stat snapshot to the stat snapshot as of the previous integration batch (batch boundaries per the canonical-source rule in "Recent Additions" below). Simplest implementation: store each analysis run's summary stats (tiny JSON), keyed by the git-derived batch it corresponds to, in a local cache file the GUI writes to (NOT AudioMirror, NOT the library - a new small file under the GUI's own folder, e.g. `gui/.cache/stats-history.json`), and diff against the second-most-recent batch entry. This is the "batch-shaped comparison" pattern from Last.fm's "+7% vs 2024" tile - only meaningful against batch data, never a fabricated daily/monthly delta.

**File size / Date Added note - decided, don't re-open:** use the XML file's own mtime and a proxy size (e.g. estimate from bitrate x length, or omit exact size and show "~" if no cheap proxy exists). **Do not resolve real MP3 paths under `C:\Users\David\Audio\` for this** - at 5,693 tracks, per-track disk stat calls on every load/refresh is a real performance risk with no caching layer to absorb it, and it's not worth building one just for a size column. Note this choice in `GUI-ROADMAP.md` as already made, not as an open decision.

### Panels

**Genre Distribution** - donut/pie/treemap swappable via dropdown (pattern already in mockup.html, port directly). Source: `Genres` field, semicolon-split, first genre per track counted (or count all genres a track has - your call, note which).

**Decade Distribution** - bar/donut swappable via dropdown. Source: `Year` field bucketed to decade.

**Year Distribution** - horizontal bar, top years by track count. Source: AudioReport.md "Year Statistics" table - port directly, all 19+ rows or top N with a "show all" affordance.

**Genre Balance (radar)** - same genre-share data as the donut, shown as a radar for a different read. **Contrast fix, mandatory:** ApexCharts (or whatever chart library you use) does not inherit page CSS variables for axis/legend/tooltip text - it needs explicit label color config (in ApexCharts: `yaxis.labels.style.colors`, `xaxis.labels.style.colors`, `legend.labels.colors`, and `tooltip.theme:'dark'`). Round-1's radar chart shipped with white-on-white axis numbers because this wasn't set. Apply this explicit theming to **every** chart panel's axis/legend/tooltip, not just the radar - it's a systemic gap in chart-library theming vs. page theming, not a one-off bug.

**Top Artists** - horizontal bar, toggle between the two AudioReport.md rankings: "Artists Excluding Musivation" and "Artists All" (these differ meaningfully - Akira The Don dominates the "All" ranking via the Musivation motivational-audio collection but is absent from "Excluding Musivation"). Port both tables directly from the report/JSON output.

**Recent Additions** - grouped by integration batch, NOT by relative day ("2 days ago"). Header row per batch: "Batch <date> (<N> tracks)", then the tracks added in that run underneath. **Batch boundaries: use AudioMirror commit history as the single canonical source everywhere a "batch" is shown** (Recent Additions, the delta tiles, the batch bar chart) - it's the more authoritative record since it predates the GUI's own cache and covers every past integration run, not just ones since the GUI started running. `stats-history.json` stores the stat *values* for each batch (keyed to the same git-derived batch boundaries), not an independent definition of where batches start and end - two different batch-boundary sources across panels would make "Batch <date>" headers disagree with the delta tiles' notion of "last batch."

**Tracks Added Per Integration Batch** - bar chart, one bar per batch, last 6-10 batches. This replaces round-1's daily "Additions Calendar" heatmap, which was flagged as fundamentally wrong for this data shape (see the cadence note at the top of this brief). Source: same batch boundaries as Recent Additions.

**Track Age Distribution** - bar chart, buckets like `0-2y / 2-5y / 5-10y / 10-20y / 20y+` computed from `Year` field vs current year. New this round, pairs with the scalar Age Statistics (avg/median/newest/oldest) from AudioReport.md - show those as a small stat callout near the chart or as part of the stat tile row if space allows.

**Cover Art Resolution Breakdown** - bar chart of the `AlbumCover` Width x Height buckets, e.g. "800x800", "1200x1200", "1000x1000", etc. Source: AudioReport.md "Cover Art Statistics" line (`Top dims: 800x800=3080, 1200x1200=1711, ...`) - parse this or recompute directly from AudioMirror XML `AlbumCover` fields (more robust than parsing the text report).

**Tag Completeness ring** - radial/circular progress widget (WakaTime's "AI-driven %" ring, Last.fm's "listening clock" - both are single circular-proportion widgets). Show % of tracks with all core tags present (Title/Artist/Album/Genre/Year all non-empty/non-"Missing"). This is a reusable component - build it once, use it for both rings below.

**High-Res Cover Art Coverage ring** - same radial widget, % of tracks with cover art >= 800px (i.e. `AlbumCover.Width >= 800`). Source: AudioReport.md "Cover Art Statistics" (`Sub-800px: N`) - coverage = (total - sub-800px) / total.

**Global date-range filter** - a dropdown (All time / current year / previous year / last integration batch) in the Statistics page header that re-renders the dashboard against the selected window. Mockup demonstrates this pattern on the Year chart only, as a proof of concept; extend it to apply consistently across the panels where a date window is meaningful (Year/Decade/Age/batch charts - not Genre/Cover-art breakdowns, which aren't time-windowed in a meaningful way).

### Configurability pattern (generalize, don't stop at Genre)

Round 1 only made Genre swappable. This round: Genre (donut/pie/treemap), Decade (bar/donut), Top Artists (Excl. Musivation / All toggle), plus the global date-range filter. If you build additional panels beyond this list, prefer giving them a similar toggle/swap control over a fixed single view - it's a cheap addition once the pattern exists and matches David's "more configurability across all panels" feedback.

### Stretch, if budget allows

- Artist treemap (hierarchical genre -> artist share)
- Library size vs track count dual-axis chart over batches
- Year-over-year comparison toggle
- Top-10-vs-long-tail split visualization

---

## Chart library and stack decision (already researched - build, don't re-research)

**Stack: your call, same reasoning as round 1** - see below, unchanged from round-1 research (still current as of 2026-07-02):

- **NiceGUI** (Python-native, FastAPI + Vue/Quasar under the hood, WebSocket live updates, `ui.echart`/`ui.plotly` wrappers) - **recommended.** Write UI in pure Python, no separate JS build step, easiest to wire real backend actions (subprocess calls, file reads) directly into UI event handlers. Its `ui.echart` wrapper exposes Apache ECharts, which has every chart type this brief needs built in (radar, bar, donut/pie, treemap, radialBar/gauge equivalent) plus room to grow.
- **Flask + ApexCharts/Chart.js (JS frontend)** - safe fallback if NiceGUI's component model doesn't fit something. The mockup's ApexCharts code is directly portable to this stack (mockup uses ApexCharts via CDN specifically so it's a drop-in reference regardless of which stack wins).

**Chart-type choices for this brief, with reasoning** (so you don't need to re-derive them):
- **Donut/pie/treemap** for Genre - three different reads on hierarchical/proportional share data, cheap to make swappable since they share the same series shape.
- **Bar (vertical)** for Decade/Year/batch/age/cover-resolution - all are simple categorical counts, bar is the least ambiguous chart type and reads correctly at a glance.
- **Horizontal bar** for Top Artists and Year - long text labels (artist names, "2010s" style categories) read better unrotated on a horizontal axis.
- **Radar** for Genre Balance - deliberately a second read on the same data as the donut; radar makes uneven genre spread visually obvious in a way a donut doesn't.
- **RadialBar/circular progress ("ring")** for Tag Completeness and Cover Coverage - both are single-proportion metrics (% complete), which is exactly what this chart type is for (WakaTime's AI% ring, Last.fm's listening-clock ring - both single-proportion circular widgets, not general-purpose charts).
- Explicitly rejected: calendar/scrobble heatmap (wrong shape for batch data, see cadence note), dual-axis growth chart (deferred to stretch - real value once there's more batch history to show a meaningful trend), sankey/flow diagrams (no natural source-to-destination flow in this data - library growth isn't a flow).

---

## AudioMirror XML schema (Statistics + Library Browser data source)

Location: `C:\Users\David\GitHubRepos\AudioMirror\AUDIO_MIRROR\` - one `.xml` file per track, in subfolders (`Artists/<ArtistName>/<Album>/`, plus `Compilations/`, `Miscellaneous Songs/`, `Motivation/`, `Musivation/`, `Sources/`). Recursively glob `*.xml`.

```xml
<?xml version="1.0" encoding="utf-8"?>
<Track>
  <Title>see the real</Title>
  <Artists>21 Savage</Artists>          <!-- semicolon-separated if multiple, first = primary artist -->
  <Album>american dream</Album>
  <Year>2024</Year>
  <TrackNumber>10</TrackNumber>
  <Genres>Rap; Hip Hop</Genres>          <!-- "; " separated -->
  <Length>00:03:02.6250000</Length>      <!-- .NET TimeSpan format, hh:mm:ss.fffffff -->
  <AlbumCover><Count>1</Count><Width>1200</Width><Height>1200</Height></AlbumCover>
  <Compilation>True</Compilation>        <!-- "True" or "False" string -->
</Track>
```

All fields are required (strict schema, locked 2026-06-06) - no defensive parsing needed for missing fields. Full reference: `AudioManager\docs\References\AudioMirror-Format.md`.

---

## Library Browser tab: full column + view spec

Round 1 shipped 5 columns (Title/Artist/Album/Genre/Year) and no view options. This round, build against the mockup's richer version:

**Default visible columns:** Title, Artist, Album, Genre, Year, Track Number, Length (formatted m:ss from the XML `Length` TimeSpan).

**Optional columns (iTunes-style show/hide column picker, off by default except where noted):** Date Added (from the file-size/date-added proxy decision made in the Statistics section above - reuse the same choice), Compilation (Yes/No from the `Compilation` field), Cover Thumbnail (small square rendered from the track's actual album art if you can extract it via a Python tag-reading library such as `mutagen` (pure-Python, reads embedded APIC/cover-art frames directly, no cross-language bridge needed) - a colored placeholder icon is an acceptable fallback if extracting real thumbnails is too costly for this round).

**Do not add:** Play Count, Rating, BPM, or other columns with no backing field in AudioMirror - these don't exist in the data (this was flagged explicitly in round-1 review against the iTunes reference screenshot; the iTunes column list is broader than what applies here).

**View toggle:** Table view (above) and Grid view - Sonarr-poster-style cards using each track's album cover art as the card image (real art if extracted, placeholder otherwise), with a colored status bar along the card bottom (color by genre or another meaningful dimension - your call) and an "Added: <date>" line.

**Search + filters:** full-text search across Title/Artist/Album, filter chips for genre/decade/artist (as in the mockup).

**Pagination:** 5,693 tracks needs real pagination, not one long scroll or a single unpaginated table - port the mockup's pagination row pattern (page numbers + jump-to-position input, Last.fm Library/Albums-style). **Only render the current page's rows** - query/slice the data loader's in-memory index for the current page and filters, don't send all 5,693 rows to the page and hide most of them client-side; that defeats the purpose of pagination and will feel laggy in the browser.

---

## Integration tab

Trigger `analysis`, `integrate --dry-run`, and `integrate --no-input` (real) against the real exe as subprocesses, streaming both stdout and stderr into the console panel (capture both - error output that only appears in stderr would otherwise vanish silently). **`--no-input` is mandatory on every subprocess call, including dry-run** - without it, any confirm prompt the exe emits will hang the subprocess waiting on stdin the GUI never provides. **Read the subprocess output unbuffered / line-by-line as it's produced, not all at once after the process exits** - Python subprocess pipes buffer by default, and a long `analysis` run over 5,693 tracks will otherwise make the console panel look completely frozen until the process finishes, which reads as a hang even when it isn't one. Smoke-test this against a real long-running call (e.g. `analysis --force-regen`) before considering Integration done. **Layout fix, mandatory:** the console panel must be a flex child that grows to fill available vertical space (`flex:1` on the panel, parent `main`/tab-page as a flex column) - round 1 shipped a fixed 160px console box with a large empty area below it on the page, which was flagged explicitly. Port the mockup's flex layout structure directly - it already implements this correctly.

Per-track confirm/decline queue with album art is a later pass within this tier if time allows, not required for MVP.

---

## Tag Fix tab

Build the rule-card UI from the mockup (Maintainerr-pattern: card with name, description, status badge, scope metadata row, Preview/Edit/Delete actions, top-level "+ New Rule" / "Run Rules" buttons). **This can be a functional stub** - cards render, are editable in the UI (name/description/status can change), "Preview Matches" can show a hardcoded or lightly-computed match count, but "Run Rules" does not need to execute real tag changes this round. The goal is that it looks like a real feature-in-progress, not an empty skeleton box, matching David's explicit feedback on round 1's Tag Fix tab.

---

## Mirror tab

Read-only status display: last commit SHA/date, uncommitted change count, via `git log -1` / `git status` run against the AudioMirror repo path. Read-only only - never write to AudioMirror from GUI code.

---

## Services tab (stretch goal, not required)

Round 1 left this as an empty placeholder. This round, build it as **two feasibility stub cards**, both explicitly optional and gated behind finishing the required tabs first:

**Last.fm read-only** - lowest-effort real integration available. `user.getRecentTracks` and other read endpoints work with just a free API key (`https://www.last.fm/api`, no callback URL needed) - **no OAuth flow at all**. If you have budget left after Statistics/Integration/Library/TagFix are solid, a minimal real integration here (recently-scrobbled tracks shown somewhere, or a "tracks never scrobbled" cross-reference against the library) is achievable and would be a genuine extra, not just a stub.

**Spotify read-only** - higher-effort. Needs Authorization Code + PKCE flow: an app must be registered in the Spotify Developer dashboard (David needs to do this himself - it's his account), with a `localhost` redirect URI, then a code-verifier/code-challenge exchange for an access token. No client-secret storage risk (PKCE is designed for exactly this: desktop/local apps that can't safely hold a secret), but meaningfully more setup than Last.fm. Only attempt after Last.fm is working and only if there's clear budget remaining.

**Keep TIER G5 in GUI-ROADMAP.md as "far future"** - this section is an invitation to go further if the core tabs finish early, not a scope change to the roadmap's tier structure.

---

## Where to put the code

New top-level folder in this repo, e.g. `AudioManager\gui\`. Keep it fully separate from the CLI's `project\AudioManager\` C# solution so you don't risk breaking the CLI build. Add a short "how to run it" README inside `gui\` or a section in the main repo README. Do NOT read, parse, or modify `project\AudioManager\AudioManager.csproj`, `Program.cs`, or anything in `project\AudioManager\Code\` - you don't need to; the exe's existing CLI surface (documented above) is your integration point, and the C# solution is out of scope for this session entirely.

**Commit frequently** - one commit per tab/panel completed, not one commit at the end. See `contingency-plan.md` in this folder for why (usage-limit pauses preserve session state, but only committed work survives a hard kill).

---

## Go beyond the mockup

The mockup is the **structural floor** - tab layout, dark theme, panel grid, chart selection, interaction patterns (column picker, view toggle, rule cards, radial rings). It is explicitly **not** the polish ceiling. David's own framing: "go FAR BEYOND the mockup, plus ultra." Once the structure above is locked in and the required tabs are functionally real, you have license to exceed the mockup's visual richness - better spacing, micro-interactions, loading states, empty states, animation on chart transitions, whatever makes it feel like a finished product rather than a wired-up wireframe. Don't spend this license before the structural requirements above are met; spend it after.

The `frontend-design` plugin skill is enabled for this session. Use it **only** for this polish pass - typography pairing, spacing/weight extremes, background depth (the mockup's system-font stack `Segoe UI/Roboto/Arial` and uniform 6-8px radius are exactly the kind of generic default it exists to catch). Do **not** let it override the mockup's already-approved dark theme, tab structure, chart-library choice, or accent-color system - those were locked through two rounds of human review and are not open for reconsideration.

---

## Definition of done

- GUI loads (browser or desktop window, per your stack choice) and shows all six tabs; Statistics and Integration are fully functional against real data/real subprocess calls, others may be simple/skeleton per the priority order above.
- Statistics tab includes every panel listed in the "Statistics tab: full chart/panel spec" section above, each sourced from real AudioMirror data or the exe's `analysis --json-output`, with the configurability (chart-type swaps, artist-ranking toggle, global date filter) and chart-theming (explicit axis/legend/tooltip colors on every chart) requirements applied.
- Every AudioReport.md stat category (General, Artists x2, Genre, Year, Decade, Age, Cover Art) has a visible home in the Statistics tab.
- Integration tab can trigger `analysis` and `integrate --dry-run` against the real exe, shows real output, and the console panel fills available vertical space.
- Library Browser has the full column set (default + optional via column picker), table/grid view toggle, and pagination.
- Tag Fix shows Maintainerr-style rule cards, not empty skeleton boxes.
- No writes anywhere except through the existing exe's own write paths (which already have their own safety checks) - your GUI code never writes to the library, NewMusic, or AudioMirror directly.
- `GUI-ROADMAP.md` updated: G0 decisions (stack, subprocess-vs-library) checked off with brief reasoning; file-size/date-added proxy decision noted; G1+ checklist items checked off for whatever you built.
- A short "how to run it" note so David can launch it without you.
- Report back: stack chosen and why, which tabs got full vs skeleton treatment, whether the Services stretch goal was attempted and how far it got, anything deliberately deferred.

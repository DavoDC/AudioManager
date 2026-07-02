# Brief for Fable: AudioManager GUI

**Everything you need is in this repo.** You should not need to look outside `C:\Users\David\GitHubRepos\AudioManager`. Start here, then read `AudioManager\CLAUDE.md` (build commands, safety rules, file registration quirks) and `docs\Development\GUI-ROADMAP.md` (full multi-session roadmap - this brief is the scoped, self-contained version of one work session against it).

**Window:** promotional access to Fable 5 ends 2026-07-07 11:59:59 PM PT. This is a deliberately big, well-scoped, mostly self-contained task chosen to make good use of a scarce budget.

**Visual reference (read/open before writing any UI code):** `mockup.html` in this same folder. Open it in a browser - it has working tab navigation and live (fake-data) charts across all six planned tabs. It went through a human-approval loop before you saw this brief (`PRIVATE_NOTES/memory/processes/mockup-before-fable-build.md`, workspace-internal process doc, not needed here). **Implement your GUI's visual language, layout, and tab structure against this mockup** - dark Sonarr/Radarr-style sidebar, stat tiles, panel grid, chart choices. Don't redesign it from scratch; wire real data and real actions into this shape.

---

## What AudioManager is

C# console app (.NET Framework 4.8) that manages David's personal MP3 library (~5,650 tracks at `C:\Users\David\Audio\`). It has two modes, **already exposed via CLI args on the built exe** (verified in `Code\Program.cs`):

```
AudioManager.exe analysis [--force-regen] [--json-output]
AudioManager.exe integrate [--dry-run] [--no-input] [--json-output] [--no-auto-commit]
```

The **AudioMirror** is a sibling git repo (`C:\Users\David\GitHubRepos\AudioMirror`) containing one small XML file per track - the data source for the Statistics and Library Browser tabs. Plain data on disk, not a database.

---

## Scope this session: MVP versions of ALL six tabs

Earlier planning scoped this session to Statistics only. That's been revised - **David wants to see the whole app shape this round**, not just one tab, and is comfortable with simple/skeleton versions of the later tiers as long as Statistics and Integration are functionally real. Build in this priority order, and stop moving down the list once you're running low on session budget - a fully-working Statistics + Integration with skeleton Library/TagFix/Mirror/Services is a good outcome; don't sacrifice Statistics/Integration quality to rush the rest.

1. **Statistics** (real, full-featured - this is the centerpiece, see chart requirements below)
2. **Integration** (real - trigger the exe's existing `analysis` / `integrate --dry-run` / `integrate` modes as subprocesses, stream output into a console panel in the GUI)
3. **Library Browser** (simple MVP - browse/search/filter over the same AudioMirror data already loaded for Statistics)
4. **Tag Fix** (skeleton acceptable - rule-builder UI can be a stub/placeholder with the intended interaction sketched but not necessarily wired to real rule execution)
5. **Mirror** (skeleton acceptable - read-only status display: last commit SHA/date, uncommitted count, from `git log`/`git status` run against the AudioMirror repo path, read-only)
6. **Services** (placeholder tab only, explicitly far-future - do not build functionality)

### Why subprocess, not core-library extraction

The roadmap doc's TIER G0 lists "extract a shared core C#/GUI library" as a recommended architecture decision. For this session, **don't do that refactor.** The exe already accepts the args above and already has `--json-output` support - shelling out to the existing binary and parsing its output is lower-risk, zero-change-to-CLI-code, and gets you working Integration today. Document this as the G0 decision for now in `GUI-ROADMAP.md` (check off the item, note "subprocess invocation chosen for MVP; revisit core-library extraction only if a future tier needs deeper C# logic access than the exe's CLI surface provides").

### Safety constraints (read `CLAUDE.md` for the full list)

- The music library (`C:\Users\David\Audio\`) and NewMusic inbox are **not backed up**. Never write to them directly from GUI code - the only writes that happen are through the existing exe's own integrate mode, which already has its own safety checks (dry-run, confirm flow). Your GUI code triggers the exe; it doesn't reimplement its logic.
- Never write to the AudioMirror repo directly either, except read-only `git log`/`git status` for the Mirror tab.
- If unsure whether an operation is read-only or safe, don't do it - note it in your summary instead of guessing.

---

## Statistics tab: chart requirements (expanded, research-backed)

David wants this to be genuinely impressive - "should make me say WOW" - configurable, and showing the data multiple ways, not just six flat charts. Research on comparable tools below; use it, and then **do a brief brainstorm of your own before building**: note 2-3 additional stat/chart ideas you'd add if time allows, write them in `GUI-ROADMAP.md` under TIER G1, and build the ones that fit your time budget.

**What Spotify Wrapped / Last.fm-style tools commonly show** (via David's Sonnet research, 2026-07-02):
- Top artists/tracks/genres by count or share, with percentage-style bars alongside raw counts
- Monthly/seasonal breakdowns (listening or library-growth patterns over the year)
- Date-range selection and year-over-year comparison, not just all-time totals
- Pie/donut/bubble charts for top-N breakdowns; treemaps for hierarchical share (genre -> artist)
- Calendar/scrobble heatmaps (GitHub-contributions-style grid) for activity-over-time at a glance
- Colored "wave" or timeline graphs for listening/library history

Sources: [rigtch.fm Spotify Stats](https://rigtch.fm/blog/spotify-stats-2026), [bijou.fm Last.fm Dashboard](https://www.bijou.fm/tools/visualization/last-fm-dashboard), [awesome-lastfm tool list](https://github.com/jnguyen1098/awesome-lastfm)

**Required panels for G1 (baseline, from the original scope):**
- Genre distribution, Decade distribution, Top artists, Library totals, Recent additions, Frequency/growth over time

**New this round - at minimum, add these (already prototyped in mockup.html):**
- Genre balance as a **radar chart**, not just pie - different read on the same data
- **Additions calendar heatmap** (last N weeks, day-of-week x week grid) - scrobble-heatmap style
- **Configurable chart type per panel** - at minimum the Genre panel should let the user swap between Donut/Pie/Treemap live, same underlying data. Generalize this pattern to other panels if time allows (e.g. a global date-range filter that re-queries/re-renders everything).

**Stretch, if budget allows (brainstorm your own too):** artist treemap, "library growth vs total size" dual-axis chart, year-over-year comparison toggle, top-10 vs long-tail split.

---

## Web stack (your call - here's the research to decide fast, don't re-research from scratch)

Comparison researched 2026-07-02 (see sources below). You own this decision (TIER G0), pick fast and move on:

- **NiceGUI** (Python-native, FastAPI + Vue/Quasar under the hood, WebSocket live updates, built-in `ui.echart`/`ui.plotly` wrappers) - write UI in pure Python, no separate JS build step, easiest to wire real backend actions (subprocess calls, file reads) directly into UI event handlers. Best fit if you want ALL SIX TABS built fast with real interactivity (buttons that trigger the exe, live console output) without juggling two languages.
- **FastAPI + HTMX + Plotly** - more manual control over markup, still Python-first, slightly more boilerplate per interactive element than NiceGUI.
- **Flask + ApexCharts/Chart.js (JS frontend)** - simplest mental model, matches the mockup file directly (mockup uses ApexCharts via CDN), but means writing and maintaining two languages (Python backend + JS frontend) for a solo-maintained app.

**Recommendation, not a mandate:** NiceGUI is the best fit for "flashy, configurable, all tabs, real trigger actions, mostly local viewing today, maybe hosted later, Python-only so David can maintain it" - it gets you interactive multi-tab real actions fastest in one language. If you pick it, its `ui.echart` wrapper exposes Apache ECharts, which has the deepest chart-type library (radar, heatmap, treemap, sankey, all built in) if you want to go beyond what's in the mockup. If NiceGUI's component model doesn't fit something you need, Flask+ApexCharts is a safe fallback - the mockup's chart code is directly portable to it.

Sources: [NiceGUI GitHub](https://github.com/zauberzeug/nicegui), [NiceGUI docs](https://nicegui.io/documentation), [FastAPI+HTMX dashboards](https://medium.com/codex/building-real-time-dashboards-with-fastapi-and-htmx-01ea458673cb), [JS charting library comparison 2026](https://lalatenduswain.medium.com/the-complete-guide-to-javascript-charting-libraries-in-2026-choosing-the-right-visualization-tool-dac9aeb15f60)

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

Full reference: `AudioManager\docs\References\AudioMirror-Format.md`. No file-size field in the XML - for "total size" and "recent additions," either resolve the real MP3 path under `C:\Users\David\Audio\` (read-only, never write) for exact size/mtime, or use the XML file's own mtime as a proxy. Either is defensible for MVP; note your choice in `GUI-ROADMAP.md`.

---

## Where to put the code

New top-level folder in this repo, e.g. `AudioManager\gui\`. Keep it fully separate from the CLI's `project\AudioManager\` C# solution so you don't risk breaking the CLI build. Add a short "how to run it" README inside `gui\` or a section in the main repo README. Do NOT modify `project\AudioManager\AudioManager.csproj`, `Program.cs`, or anything in `project\AudioManager\Code\` - you don't need to; the exe's existing CLI surface is your integration point.

---

## Definition of done

- GUI loads (browser or desktop window, per your stack choice) and shows all six tabs; Statistics and Integration are fully functional against real data/real subprocess calls, others may be simple/skeleton per the priority order above.
- Statistics tab includes at minimum the baseline G1 panels plus the radar chart, calendar heatmap, and at least one configurable/swappable chart panel.
- Integration tab can trigger `analysis` and `integrate --dry-run` against the real exe and show real output.
- No writes anywhere except through the existing exe's own write paths (which already have their own safety checks) - your GUI code never writes to the library, NewMusic, or AudioMirror directly.
- `GUI-ROADMAP.md` updated: G0 decisions (stack, subprocess-vs-library) checked off with brief reasoning; your own brainstormed chart ideas noted; G1+ checklist items checked off for whatever you built.
- A short "how to run it" note so David can launch it without you.
- Report back: stack chosen and why, which tabs got full vs skeleton treatment, what extra chart ideas you brainstormed and which you built, anything deliberately deferred.

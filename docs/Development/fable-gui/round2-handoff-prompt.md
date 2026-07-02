ated 2026-07-02. Paste this as the opening prompt of a new Sonnet session (this session's context is high, hence the handoff).

---

# Task: revise the AudioManager GUI mockup and write the final Fable brief

You're picking up round 2 of a GUI planning effort for AudioManager (David's personal music library tool). Round 1 produced `docs/Development/fable-gui/mockup.html` (fake-data HTML mockup) and `fable-brief.md` (the brief for Fable 5 to build from). David reviewed the round-1 mockup and gave detailed feedback - none of it has been applied yet. **Your job: apply the feedback to the mockup, do the research David asked for, then rewrite `fable-brief.md` into a final, self-contained version Fable can build from without further research.** Fable's job is to build, not to research - front-load everything into the brief.

Read first, in order:
1. `AudioManager/docs/Development/fable-gui/fable-brief.md` (round 1 brief - your starting point)
2. `AudioManager/docs/Development/fable-gui/mockup.html` (round 1 mockup - open in a browser)
3. `AudioManager/docs/Development/GUI-ROADMAP.md` (durable long-term reference)
4. `AudioManager/CLAUDE.md` (safety rules, and the batch-integration-cadence note added 2026-07-02 - important, see feedback item 1 below)
5. `AudioManager/docs/Development/fable-gui/contingency-plan.md` (usage-budget handling, already written - don't redo this)
6. `PRIVATE_NOTES/memory/processes/mockup-before-fable-build.md` (workspace process doc - the loop you're now running: revise mockup, get approval, then finalize brief)

## Feedback to apply (round-1 mockup review, verbatim intent preserved)

1. **"Additions Calendar (last 12 weeks)" heatmap is wrong and must be removed or redesigned.** David integrates new music in one big batch every 2-4 weeks, not a little each day - a daily heatmap will be empty almost every day and spike hard on integration day, which looks broken, not insightful. This is already documented in `AudioManager/CLAUDE.md` (search "batch-integration cadence"). Replace it with something that actually reads well against batch-shaped data - e.g. a per-integration-run/per-week bar chart, or a timeline of batch events. Apply the same "does this assume daily activity?" audit to every other panel in the mockup - the "Recent Additions" table showing "2 days ago / 4 days ago / 6 days ago..." has the same false-daily-cadence smell; reconsider whether relative-day labels make sense here or whether grouping by batch/week reads better.

2. **"Library Growth (tracks added per month)" needs real range controls**, not just a fixed recent window - go back as far as the data allows (all-time), with a year selector and date-range picker, comparable to how Last.fm's Library/Reports pages let you pick "All time / 2026 / 2025 / ..." (see `TEMP/7fa04bc4-cf3e-4897-846c-c567e22dd83d.png` and `TEMP/19fe23f3-2981-40a3-9147-9e154d79fb94.png` date-range selector in the top right).

3. **White text is unreadable somewhere in the mockup** (flagged on a screenshot, exact panel not preserved in this handoff - re-check every panel's text/background contrast against the dark theme CSS variables at the top of `mockup.html`, especially anywhere text sits on a light or mid-tone fill rather than the dark panel background). Dark mode colors overall were approved as good - this is a contrast bug in one spot, not a theme change.

4. **More configurability across ALL panels, not just Genre.** Round 1 only made the Genre panel's chart type swappable (donut/pie/treemap). Generalize this pattern - more panels should have swappable chart types and/or filter controls. Add a global date-range filter that re-renders the whole dashboard, not just individual panels.

5. **More chart types on the Statistics page overall** - go beyond the round-1 baseline (genre pie/donut/treemap, decade bar, top artists, radar, growth line, calendar heatmap-being-replaced). Do real research (see Research section below) rather than guessing, and justify each addition against David's actual data shape (batch-integrated personal MP3 library, not a streaming service with daily listening events).

6. **Surface far more metadata.** The AudioMirror XML schema (`AudioManager/docs/References/AudioMirror-Format.md`) has fields the mockup doesn't show anywhere: TrackNumber, AlbumCover dimensions/count, Compilation flag. The Library Browser table currently has 5 columns (Title/Artist/Album/Genre/Year) - add more (track number, length, compilation flag, cover art dimensions or a thumbnail) and add filters/sortable columns, similar to how Last.fm's Library > Albums view sorts by scrobble count with a visible rank and supports pagination (`TEMP/bc479e27-48a0-43e2-996f-109c67befd79.png`).

7. **Every stat in an AudioReport.md must appear in the Statistics GUI.** Read a real report end to end, e.g. `AudioManager/reports/2026/2026-06-28 - AudioReport.md`, and cross-check the mockup against it. Missing from round 1: total playback hours, average/median song length, total library size (mockup has a "Total Size" tile but verify it matches the report's method), average file size, **two separate artist-ranking lists** (Artists Excluding Musivation vs Artists All - these differ meaningfully, both need a place in the UI, e.g. a toggle), Year statistics (not just Decade), Age statistics (average/median track age, newest/oldest with year), and Cover Art statistics (has-cover count, no-cover count, unknown-format count, sub-800px count, non-square count, breakdown by resolution e.g. "800x800=3080, 1200x1200=1711..."). Genre and Decade are already covered - keep them, add the rest.

8. **The GUI shell should fill the browser window**, not render as a small scrolling box within a larger empty page (flagged on a screenshot - check `main{flex:1;...}` and the `.shell{min-height:100vh}` rule actually take effect, and that nothing constrains the outer container's height/width below full viewport).

9. **Visual/UX inspiration - go look at these, don't just take David's word for the vibe:**
   - **iTunes** library/media-browser UI (classic multi-pane browser with sidebar + list + detail).
   - **Sonarr / Radarr** (already the stated design direction - dark sidebar, media cards, status badges).
   - **Maintainerr** (https://maintainerr.info/) - David specifically liked this UI on sight; research it online (screenshots, docs site) and identify what specifically reads as clean/modern about it - card layout, spacing, badge/status conventions - and where AudioManager's shape can borrow from it.
   - **Last.fm** and **WakaTime** dashboards - reference screenshots are in `PRIVATE_NOTES/TEMP/*.png` (three files - Last.fm yearly report page with stat tiles/weekly-scrobbles bars/listening-clock radial/top-tags streamgraph/artist map/decade bars/music-ratio donut/listening-fingerprint radar/quick-facts tiles; WakaTime dashboard with per-project time bars, AI-coding stat ring, category donuts, weekday bar chart, project card grid; Last.fm Library/Albums sortable-ranked table with date-range filter and pagination). Pull concrete panel ideas from these, not just "make it look nice" - e.g. WakaTime's per-project horizontal time-bar list is a good pattern for per-artist or per-genre breakdowns; Last.fm's stat-tile row with a comparison delta ("+7% vs 2024") is a good pattern if there's a meaningful prior-period comparison to show against batch data.
   - Do a genuine web research pass (WebSearch/WebFetch) beyond these four references - look for other music-library/self-hosted-dashboard UIs in the Sonarr/Radarr/Maintainerr family and note anything else worth adopting.

10. **Services tab: sketch a real (if minimal) integration, don't leave it purely as a placeholder.** David floated Fable attempting a basic Spotify or Last.fm integration for this tab. Research feasibility (API surface, auth flow complexity, what a minimal read-only integration would need) and write it into the brief as an explicit **stretch goal** Fable can attempt if Statistics/Integration/Library/TagFix/Mirror are done with budget to spare - not a requirement, and don't let it distract from the core tabs. Keep `GUI-ROADMAP.md`'s TIER G5 framing (far future) as the floor; this is about whether Fable should be *invited* to go further this round, not about moving the roadmap tier.

11. **Overall bar: "should make me say WOW."** David used the phrase "go FAR BEYOND the mockup, plus ultra" - the mockup is the structural floor (tab layout, dark theme, panel grid), not the ceiling. The final brief should explicitly tell Fable it has license to exceed the mockup's polish and richness, once the structural shape and the specific fixes above are locked in.

## Research task: figure out when to stop

David flagged this explicitly - don't let mockup revision become an unbounded polish loop before Fable even starts. Before finalizing, define and write into the brief (or a short section of this doc) **explicit stopping criteria** for your own research/mockup pass - e.g. "N chart-type candidates evaluated and either adopted or rejected with a one-line reason," "every AudioReport.md stat has a named home in the mockup," "every feedback item above has a mockup diff or an explicit brief instruction." Once those are met, stop iterating and present to David for approval - don't keep refining past that.

## What "done" looks like for this session

1. `mockup.html` revised: batch-cadence chart fixed, contrast bug fixed, full-height shell fixed, more configurable panels, more chart types (research-backed), more metadata columns in Library Browser, all AudioReport.md stats have a panel.
2. Send the revised mockup to David for approval (`SendUserFile`, `display: render`) - **do not skip this loop**, it's the whole point of mockup-first (see `mockup-before-fable-build.md`). Iterate if he wants changes.
3. Once approved, rewrite `fable-brief.md` (or write `fable-brief-round2.md` and update the pointer in `GUI-ROADMAP.md` / `PRIVATE_NOTES/memory/project/fable-5-promo/opportunities.md`) as a final, self-contained brief: embed your research findings (chart type choices with reasoning, Maintainerr/iTunes/Last.fm/WakaTime takeaways, Services-tab feasibility notes) directly in the brief text so Fable doesn't have to go research anything - Fable's job this round is building, not discovering.
4. Confirm the contingency plan and 1M-context decision (`contingency-plan.md`) are still accurate / get David's explicit call on 1M context before handoff to Fable.
5. Report back: what changed in the mockup, what you decided against and why, the final chart-type list, and confirmation the brief is ready to hand to a Fable session.

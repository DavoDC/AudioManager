# Ideas & Future Work

Single source of truth for all pending work. **CLI only.** GUI planning: `GUI-ROADMAP.md`. Completed items -> `HISTORY.md`.

Items are tiered by priority. Do not advance to the next tier until the current tier is verified on real data.

---

## TIER 1 - BLOCKING

**Goal: deliver auto-routing for known cases - eliminate confirmation fatigue. Prerequisites must be verified on real data first.**

**RETROSPECTIVES (COMPLETED - see HISTORY.md for details)**

**Quick win:**

- [ ] **Mark historical workflow docs as frozen** - The `docs/Historical/WorkflowExecution-2026-04-26/` folder documents the April 26 workflow planning sessions. These docs are now stale (decisions made, execution complete). Add a header note to `docs/Historical/WorkflowExecution-2026-04-26/README.md` (create if needed): "FROZEN - Historical record of pre-execution planning from 2026-04-26. Do not edit. Live improvements documented in IDEAS.md and git history." Prevents future maintenance attempts.

**Prerequisites for reliable auto-routing:**

- [ ] **(2.2) Character validation & sanitization pass** - [HIGH confidence] Blocker A root cause: Windows path validation failed on "?" in album tag (WHAT IF?). Current fix (SanitiseFolderName) is targeted but incomplete. Fix scope: audit ALL Windows illegal characters (?, /, \, :, *, <, >, |, "), create character-safety test cases, validate all 5531 tags. Payback: prevents integration crashes from character edge cases. Evidence: real integration found case (May 3 23:36); dry-run missed it. Implementation notes in FEEDBACK-Stage3C.md lines 29-41.

- [ ] **(2.1) TagFixer: Comprehensive suffix stripping** - [HIGH confidence] Blocker B2 root cause: Shaggy track had " (International Version)" in album field, caused routing to album subfolder instead of Singles. User manually fixed via MP3tag. Fix scope: strip ALL common album-folder suffixes (parentheticals, edition markers, remaster markers, year suffixes) before tagging. Test cases exist (real integration data). Payback: prevents false-positive routing and LibChecker violations from metadata corruption - directly affects auto-routing reliability. Implementation notes in FEEDBACK-Stage3C.md lines 59-70.

**Target - user's top priority:**

- [ ] **Auto-routing for known cases - eliminate confirmation fatigue** - [HIGH confidence] Current rules (artist folder mapping, genre overrides, album subfolder logic) are validated by Stage 3C real integration (5531 songs, 502 routing decisions). Implement confidence gates in GetDestDir(): (1) artist folder exists -> auto-route (certain), (2) Musivation artist -> auto-route to Musivation (certain), (3) 3+ songs from existing album -> auto-route to album subfolder (likely). **NO confirmation prompts for certain/likely routes** - routing happens silently with decision logged. Only prompt for uncertain cases (new artist, ambiguous metadata, edge cases). Mark each route with confidence level (certain/likely/uncertain) and log ALL decisions to XML for audit trail. Impact: transforms integration from "confirm N files" to "handle N files" - eliminates decision fatigue on routine routing. Implementation: update GetDestDir() to accept confidence thresholds as parameters, add routing confidence enum, wire into MusicIntegrator decision loop.

- [ ] **Routing proposal UX: three quick wins from Stage 3C feedback** - User observed these pain points during integration. All quick wins, high impact on scanning/decision speed:
  - **(1) Split `Proposed:` into readable + filesystem path:** Currently one long `Proposed: Musivation\Akira The Don\Singles\...` line. Split into: `Proposed: Akira The Don / Singles` (short) + `Path: Musivation\Akira The Don\Singles\...` (full).
  - **(2) `Reason` field should explain WHY, not restate:** Currently mirrors `Proposed:` line (e.g. "Akira The Don -> Singles"). Replace with actual logic (e.g. "3+ songs from album -> album subfolder", "artist folder exists -> auto-route").
  - **(3) Fix concise proposal positioning:** Summary line `-> Artist / Folder` appears above `Proposed:` path, feels out-of-order. Optimize ordering for scannability (layout to be determined at implementation time).
  - **Impact:** Fixes user experience friction observed during Stage 3C. Makes routing decisions scannable at a glance instead of parsing full paths.

- [ ] **TagFixer output formatting: blank line between SKIPPED and FIXED entries** - [quick win] Symptom: TagFixer output has inconsistent spacing - blank lines separate most [FIXED] entries but are missing between [SKIPPED] and following [FIXED] entries. Root cause: output generation doesn't ensure separators after SKIPPED entries. Fix: ensure all [FIXED]/[SKIPPED] blocks are consistently separated by blank lines for readability.

---

## TIER 2 - QUALITY

**Goal: improve UX, add test coverage, and audit metadata quality. Start after TIER 1 prerequisites are verified.**

- [ ] **Fix report table formatting - markdown tables instead of plain text** - [quick win] Current reports (`reports/2026/YYYY-MM-DD - AudioReport.md`) render statistics tables as broken plain text (raw columns, no markdown formatting), making them unreadable on GitHub and difficult to scan. Root cause: `ReportWriter.cs` (and/or `Analyser.cs`) emits plain text, not markdown. **Two-part fix:** (1) Modify code to emit proper markdown tables with pipe-delimited columns and header separators (e.g. `| Header | Header |` + `|--------|--------|`). (2) Regenerate all historical reports (2026-05-05 and earlier) using the corrected code to restore archival records. Payoff: reports become usable for data review and archival.

- [ ] **Performance investigation - Parser is slow** - User-visible pain point: Parsing 5516 tags took 38.124 seconds (~6.9ms per tag). During runs, user perceives long hangs with no feedback. **Suspected bottleneck:** Reading thousands of MP3 file metadata (not XML parsing). Profile to confirm and benchmark against alternatives (streaming vs. batch loading, parallel processing per artist folder, etc.). Once root cause identified, implement optimization. High payback - runs should feel responsive. Implementation notes in FEEDBACK-Stage3C.md lines 280-286.

- [ ] **Metadata audit of existing library** - [HIGH confidence] Pattern finding from Stage 3C: 3 blocker types (character, casing, suffixes) suggest systematic brittleness in existing library metadata. Fix scope: scan existing AudioMirror XMLs for casing inconsistencies, character illegality, metadata-vs-folder mismatches. Produce violations report. Payback: find edge cases BEFORE next batch integration; complements character validation (2.2) by revealing existing library state. Implementation notes in FEEDBACK-Stage3C.md lines 342-345.

- [ ] **Add minimal automated tests for three broad features** - TDD approach for three core features only. ROI analysis showed full test suite expansion not worth it; strict scope discipline prevents token waste.
  - **Motivation:** Each session requires multiple manual dry runs and force regens to verify fixes. Tests for key features catch regressions immediately at build time, enabling faster iteration. Current feedback: each fix session needs 2-3 manual verification cycles (dry run, force regen, spot-check). Tests eliminate this.
  - **Testing strategy:** Write tests FIRST (TDD), implement features to pass them. Once all three features have green tests, STOP - do not expand beyond these three unless a regression is found that tests didn't catch. Rule: expand test suite only when real bugs escape testing, not speculatively.
  - **Feature 1: Build and launch** - Program compiles cleanly via MSBuild and runs without crashing (e.g. `--help` works). Catches compilation/linkage regressions immediately. Payback: weeks.
  - **Feature 2: Tag normalization** - TagFixer correctly cleans and normalizes tags: TCMP flags set, artist casing preserved (mike. stays lowercase), genre set per rules (Musivation genre for Musivation artists), suffixes stripped (album field cleaned). Payback: 2-3 weeks (catches tag mutation regressions that fail in LibChecker days later).
  - **Feature 3: Routing correctness** - GetDestDir() produces correct destination paths given artist, album, and library state. Sample cases: 3+ songs from album -> album subfolder, 1-2 songs -> Singles, known artist folders -> existing folders, Musivation artists -> Musivation folder. Payback: 1-2 months (wrong routing = real files moved wrong - highest risk).
  - **Scope:** Focus on feature behavior, not individual function testing. Test "artist casing is preserved" not "ExtractAndFixArtists() line 47". Test "Musivation artist gets Musivation folder" not "GetDestDir() branch coverage".
  - **Skipped:** full LibChecker validation (add rules incrementally), full integration test (dry-run covers this), edge case enumeration, speculative test coverage beyond the three features.
  - Create `AudioManager.Tests` xUnit project; wire into `launch.bat` as "Run tests" menu item; broken tests block exe launch.

- [ ] **TagFixer: extend genre handling for additional artists** - Currently TagFixer sets genre to "Musivation" only for Akira The Don. Expand to:
  - **Loot Bryon Smith** -> Genre = "Musivation" (per Music-Library-Rules.md spec). ALL FILES in the Musivation folder must have the Musivation genre tag.
  - **Generic "Motivation" tracks** -> Genre = "Motivation" (currently not handled)
  - Current implementation: `ShouldFixGenre()` and `DetermineGenre()` in TagFixer.cs need extension. Once implemented, TagFixer will be 100% comprehensive.

- [ ] **Scan-ahead: show progress indicator during computation** - Symptom: User observed silence after "Scan-ahead: 4 artist(s) will hit 3-song threshold:" with no feedback, thought program hung. Root cause: scan-ahead computation takes several seconds but produces no intermediate output. Fix: print progress during scanning (e.g. "Scanning batch... (checking N artists)" or dot-tick per artist). Improves perceived responsiveness.

- [ ] **Periodic audit: ensure all artist casing rules are in config** - Artist-name-overrides.xml is the single source of truth. After major TagFixer work sessions, audit codebase (comments, CLAUDE.md, HISTORY.md) for missed rules and migrate to XML. Scott Adams rule was restored 2026-05-05 as a test case - verify no other rules were similarly lost.

- [ ] **Auto-migrate existing Misc songs when scan-ahead promotes an artist** - Currently flagged for MANUAL migration. Revisit with confirmation gate after tests exist.

---

## TIER 3 - POLISH

**Goal: close structural gaps and improve developer experience. Non-blocking.**

- [ ] **Comprehensive library audit: validate conformance, analyze patterns, unify rules** - Three tightly-coupled goals: (1) AUDIT: Scan AudioMirror XMLs against Music-Library-Rules.md, produce violations/gaps report. Confirm LibChecker catches all mandated rules. *Partial progress (2026-04-09): rules gap analysis done, CheckAlbumSubfolderRule() + CheckGenreVsFolder() added. Remaining: run full library audit, identify gaps.* (2) ANALYSE: Extract patterns from decision XMLs + AudioMirror data (artist folder distribution, album patterns, genre consistency, file counts). Build statistical models to identify high-confidence auto-routing cases. Payback: reduces manual confirmations beyond rule-based routing. (3) UNIFY: Refactor routing rules and LibChecker validation into single `RulesEngine` - rules defined once, consumed by both MusicIntegrator and LibChecker. Prevents sync failures (current: TagFixer SKIPPED but LibChecker FLAGGED because rules diverged). All three feed each other: audit finds gaps -> patterns suggest improvements -> unified rules prevent future divergence. Blocked by: auto-routing stable first (so patterns are meaningful).

- [ ] **Simplify launch.bat - move menu logic into Program.cs** - RivalsVidMaker's `run.bat` is 2 lines; all menu/mode logic lives in Python. AudioManager's `launch.bat` is 91 lines with its own menu, duplicating CLI arg logic already in `Program.cs`. Refactor: `launch.bat` becomes a ~5-line wrapper that just builds + runs `AudioManager.exe` with no args; all menu logic lives in Program.cs where it's testable and debuggable.

- [ ] **AudioMirrorCommitter: safety gates then re-enable auto-commit** - Two phases: (1) SAFETY GATES: currently skips auto-commit only if LibChecker reported hits, but doesn't account for other issues like unexpected file extensions. Update check: if Reflector found unexpected extensions OR LibChecker reported hits, skip commit and notify user. Goal: auto-commit only runs when entire pipeline is completely clean. (2) RE-ENABLE: once safety gates proven stable (several weeks of runs, zero accidental data loss), re-enable auto-commit by uncommenting logic in `AudioMirrorCommitter.TryCommit()` (lines ~60-85). Blocked by: TIER 1 all verified first. Note: manual commits are safe, re-enable is nice-to-have, not critical.

---

## TIER 4 - FUTURE

**Goal: exploratory features and advanced enhancements, tackled after core tiers are stable.**

- **Sources/ routing not implemented in GetDestDir()** - `Constants.SourcesDir` exists but `MusicIntegrator.GetDestDir()` has no routing logic for Sources/Films, Sources/Shows, or Sources/Anime. Films/Shows/Anime tracks currently fall to Misc and require manual folder-picker redirection. `Music-Library-Rules.md` documents the expected routing rules (Films subfolder = film name, Shows subfolder = show name, Anime = separate).
  - **Challenge:** Automating this is difficult - metadata alone rarely indicates source type. Current approach: if `Album` contains "OST" or "Soundtrack", prompt user for subfolder choice rather than defaulting to Misc.
  - **Exploratory:** Study existing Sources/Films, Sources/Shows, Sources/Anime tracks for metadata patterns (album names, artist names, genre, etc.) that distinguish them. May reveal rough heuristics for auto-detection, or may confirm that manual folder-picker prompt is the only reliable solution.

- **Audit libchecker-exceptions.xml - distinguish bug fixes from genuine exceptions** - Review all exceptions to identify which are workarounds for LibChecker regex bugs (e.g., "version" in legitimate titles like Alan Watts tracks) vs. genuine tracks that need exemption (e.g., Eric Thomas bonus content). For each regex-bug workaround, ensure there's a corresponding TIER 1 code improvement in IDEAS. Goal: exceptions.xml contains only track-specific needs, not rule improvements.

- **Evaluate removing .md integration logs in favour of decision XMLs** - Integration logs (`logs/integration-*.md`) and decision XMLs overlap in content. If XMLs contain all routing information, the .md logs may be redundant. Evaluate what .md logs have that XMLs don't (human-readable narrative vs structured data). If XMLs are sufficient for audit and analysis, remove .md logs to simplify the output.

- **Routing decision analysis mode** - Add a mode that reads decision XMLs, cross-references routing decisions against routing rules code and LibChecker rules, and flags inconsistencies. Produces a report: "these N files were routed to X but LibChecker would flag them as Y". Pairs well with the "Centralise rules" refactor. Exploratory - assess value after the first real integration run produces decision XML data to analyse.

- **"My Edits" tracking** - detect locally edited songs by comparing duration to official track (>3-4s diff = protected from overwrite).
- **Parody/original song pairing detection** - flag songs where a parody and its original are both in the library.
- **Album completion detection** - cross-reference library against Spotify/MusicBrainz; flag where 50%+ of an album is owned.
- **Fuzzy artist name matching** - handle artist name variations during routing ("The Beatles" vs "Beatles", featured artist formatting differences). Only matters at scale.
- **Fuzzy duplicate title matching** - extend the pre-integration duplicate check to catch near-matches (e.g. "Song (feat. X)" vs "Song", "Song - Remix" vs "Song"). Approach: normalise both sides before comparison by stripping featured artist parentheticals, stripping remix/edit/version suffixes, collapsing whitespace. Blocked by: the exact-match duplicate check must be in place first.

---

## See Also

- `docs/Development/HISTORY.md` - completed features, settled design decisions, parked ideas
- `docs/Development/GUI-ROADMAP.md` - GUI planning: webapp, tabs, Sonarr/Radarr vision, far-future integrations
- `docs/References/Music-Library-Rules.md` - canonical rules for library structure
- `docs/Historical/NewMusic-Integration-Plan-20260308.md` - past batch integration (March 2026 batch A)
- `docs/Historical/NewMusic-Integration-Plan-20260407.md` - past batch integration (April 2026)
- `docs/References/AudioMirror-Format.md` - AudioMirror XML format and repo info

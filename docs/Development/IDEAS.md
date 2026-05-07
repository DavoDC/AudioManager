# Ideas & Future Work

Single source of truth for all pending work. **CLI only.** GUI planning: `GUI-ROADMAP.md`. Completed items -> `HISTORY.md`.

Items are tiered by priority. Do not advance to the next tier until the current tier is verified on real data.

---

## TIER 1 - BLOCKING

**Goal: retrospectives complete, TIER 2 informed by findings. Harden Stage 3C learnings.**

**RETROSPECTIVES (COMPLETED - see HISTORY.md for details)**

**HARDENING (informed by retrospective root cause analysis):**

**Priority Order (from FEEDBACK-Stage3C-2026-05-07, TIER 2 recommendations):**

- [ ] **(TIER 2.1) TagFixer: Comprehensive suffix stripping** - [HIGH confidence] Blocker B2 root cause: Shaggy track had " (International Version)" in album field, caused routing to album subfolder instead of Singles. User manually fixed via MP3tag. Fix scope: strip ALL common album-folder suffixes (parentheticals, edition markers, remaster markers, year suffixes) before tagging. Test cases exist (real integration data). Payback: prevents false-positive routing and LibChecker violations from metadata corruption. Implementation notes in FEEDBACK-Stage3C.md lines 59-70.

- [ ] **(TIER 2.2) Character validation & sanitization pass** - [HIGH confidence] Blocker A root cause: Windows path validation failed on "?" in album tag (WHAT IF?). Current fix (SanitiseFolderName) is targeted but incomplete. Fix scope: audit ALL Windows illegal characters (?, /, \, :, *, <, >, |, "), create character-safety test cases, validate all 5531 tags. Payback: prevents integration crashes from character edge cases. Evidence: real integration found case (May 3 23:36); dry-run missed it. Implementation notes in FEEDBACK-Stage3C.md lines 29-41.

- [ ] **(TIER 2.3) Parser performance investigation** - [MEDIUM confidence] User-visible pain point: 38.124s for 5531 tags (6.9ms per file). Hypothesis: MP3 I/O bottleneck, not XML parsing. Fix scope: profile with instrumentation (file open time, tag read time, parsing time separately). Once profiled, propose optimization. NOT implemented until root cause confirmed. Payback: significant responsiveness improvement. Implementation notes in FEEDBACK-Stage3C.md lines 280-286.

- [ ] **(TIER 2.4) Metadata audit of existing library** - [HIGH confidence] Pattern finding: 3 blocker types (character, casing, suffixes) suggest systematic brittleness. Fix scope: scan existing AudioMirror XMLs for casing inconsistencies, character illegality, metadata-vs-folder mismatches. Produce violations report. Payback: find edge cases BEFORE next batch integration. Implementation notes in FEEDBACK-Stage3C.md lines 342-345.

- [ ] **(TIER 2.5) Routing UX improvements (3 quick wins)** - [HIGH confidence] Observed during real integration (May 3-5). (1) Split Proposed: into human-readable + filesystem path. (2) Reason field should explain WHY (routing logic) not restate destination. (3) Optimize proposal positioning for scannability. All low effort, high impact. Payback: real user friction observed. Implementation notes in FEEDBACK-Stage3C.md lines 347-350.

- [ ] **(TIER 2.6) Automated tests for three broad features (TDD)** - [MEDIUM confidence] TDD approach enforced: write tests FIRST. Strict scope: only 3 features (Build/Launch, Tag Normalization, Routing Correctness). Payback: catches regressions at build time, eliminates manual dry-run cycles. Note: Retro 2 found TDD violation (2% test commits vs 30% expected during Stage 3C). This fixes the gap. Implementation notes in FEEDBACK-Stage3C.md lines 352-356; extended TDD guidance in Retro 2.

- [ ] **Mark historical workflow docs as frozen** - The `docs/Historical/WorkflowExecution-2026-04-26/` folder documents the April 26 workflow planning sessions. These docs are now stale (decisions made, execution complete). Add a header note to `docs/Historical/WorkflowExecution-2026-04-26/README.md` (create if needed): "FROZEN - Historical record of pre-execution planning from 2026-04-26. Do not edit. Live improvements documented in IDEAS.md and git history." Prevents future maintenance attempts.

---

## TIER 2 - QUALITY

**Goal: improve UX and add test coverage. Start after first clean integration completes.**

- [ ] **Enable auto-routing for known cases - rules are solid enough** - [HIGH confidence] Current rules (artist folder mapping, genre overrides, album subfolder logic) are validated by Stage 3C real integration (5531 songs, 502 routing decisions). Implement confidence gates in GetDestDir(): (1) artist folder exists -> auto-route (certain), (2) Musivation artist -> auto-route to Musivation (certain), (3) 3+ songs from existing album -> auto-route to album subfolder (likely). Skip confirmation prompts for certain/likely routes, only prompt for uncertain cases (new artist, ambiguous metadata, edge cases). Mark each route with confidence level (certain/likely/uncertain) and log all decisions to XML for audit trail. Impact: transforms integration from "confirm N files" to "handle N files" - major workflow improvement. Implementation: update GetDestDir() to accept confidence thresholds as parameters, add routing confidence enum, wire into MusicIntegrator decision loop.

- [ ] **Performance investigation - Parser is slow** - User-visible pain point: Parsing 5516 tags took 38.124 seconds (~6.9ms per tag). During runs, user perceives long hangs with no feedback. **Suspected bottleneck:** Reading thousands of MP3 file metadata (not XML parsing). Profile to confirm and benchmark against alternatives (streaming vs. batch loading, parallel processing per artist folder, etc.). Once root cause identified, implement optimization. High payback - runs should feel responsive.

- [ ] **Routing proposal UX: three quick wins from Stage 3C feedback** - User observed these pain points during integration. All quick wins, high impact on scanning/decision speed:
  - **(1) Split `Proposed:` into readable + filesystem path:** Currently one long `Proposed: Musivation\Akira The Don\Singles\...` line. Split into: `Proposed: Akira The Don / Singles` (short) + `Path: Musivation\Akira The Don\Singles\...` (full).
  - **(2) `Reason` field should explain WHY, not restate:** Currently mirrors `Proposed:` line (e.g. "Akira The Don -> Singles"). Replace with actual logic (e.g. "3+ songs from album -> album subfolder", "artist folder exists -> auto-route").
  - **(3) Fix concise proposal positioning:** Summary line `-> Artist / Folder` appears above `Proposed:` path, feels out-of-order. Optimize ordering for scannability (layout to be determined at implementation time).
  - **Impact:** Fixes user experience friction observed during Stage 3C. Makes routing decisions scannable at a glance instead of parsing full paths.

- [ ] **Fix report table formatting - markdown tables instead of plain text** - **ESSENTIAL:** Current reports (`reports/2026/YYYY-MM-DD - AudioReport.md`) render statistics tables as broken plain text (raw columns, no markdown formatting), making them unreadable on GitHub and difficult to scan. Root cause: `ReportWriter.cs` (and/or `Analyser.cs`) emits plain text, not markdown. **Two-part fix:** (1) Modify code to emit proper markdown tables with pipe-delimited columns and header separators (e.g. `| Header | Header |` + `|--------|--------|`). (2) Regenerate all historical reports (2026-05-05 and earlier) using the corrected code to restore archival records. Payoff: reports become usable for data review and archival.

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

- [ ] **TagFixer output formatting: blank line between SKIPPED and FIXED entries** - Symptom: TagFixer output has inconsistent spacing - blank lines separate most [FIXED] entries but are missing between [SKIPPED] and following [FIXED] entries. Root cause: output generation doesn't ensure separators after SKIPPED entries. Fix: ensure all [FIXED]/[SKIPPED] blocks are consistently separated by blank lines for readability.

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

## Parked / Deprioritized

- **DECISION GATE: Python Rewrite vs .NET 8 Migration** - AudioManager is .NET Framework; could migrate to .NET 8 SDK-style (lower cost, same language, taglib#-compatible) or rewrite in Python (no build step, lightweight deps via `mutagen`). **Status:** Parked. Program works well; no immediate need to decide. Revisit only if .NET becomes a blocker or if Python advantages outweigh rewrite cost. Decision factors: token cost estimate, confirm Python libs cover TagLib# use cases, ensure file I/O scope is matched.

- **Review mode - library pruning / song-by-song decision tracking** - Add a new `review` mode that walks every song one by one, shows context (tags, folder, optional play count / popularity / lyrics), and asks: keep / remove / defer. Every decision is persisted to a config file (e.g. `config/review-decisions.xml` or similar) with: song fingerprint (artist + title or file hash), decision, date, reason. Old decisions are re-surfaced periodically (e.g. after 12 months) so the review is not one-shot - tastes change, a "keep" today may be a "remove" next year.
  - **Removal candidate signals** (surfaced in review mode to guide decisions, not auto-removed):
    - **Low play count** - cross-reference against iTunes, Last.fm, and/or Spotify listening history. A song never played in 2+ years is a prime candidate.
    - **Negative lyrical/emotional tone** - screen lyrics (via a lyrics API or local cache) and flag songs with heavily negative/depressive/aggressive content on the theory that repeated subconscious exposure shapes mood. This is subjective and must stay advisory, not automated.
  - **Auditable, reversible:** decisions live in a committed config file; a "remove" decision is the review-mode verdict, not an immediate file delete. Actual removal is a separate explicit step after review is complete, with dry-run first.
  - **Status:** Deprioritized. Revisit after core integration pipeline is stable and you have operational experience with large libraries.

---

## See Also

- `docs/Development/HISTORY.md` - completed features, settled design decisions, parked ideas
- `docs/Development/GUI-ROADMAP.md` - GUI planning: webapp, tabs, Sonarr/Radarr vision, far-future integrations
- `docs/References/Music-Library-Rules.md` - canonical rules for library structure
- `docs/Historical/NewMusic-Integration-Plan-20260308.md` - past batch integration (March 2026 batch A)
- `docs/Historical/NewMusic-Integration-Plan-20260407.md` - past batch integration (April 2026)
- `docs/References/AudioMirror-Format.md` - AudioMirror XML format and repo info

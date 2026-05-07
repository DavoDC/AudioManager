# Ideas & Future Work

Single source of truth for all pending work. **CLI only.** GUI planning: `GUI-ROADMAP.md`. Completed items -> `HISTORY.md`.

Items are tiered by priority. Do not advance to the next tier until the current tier is verified on real data.

---

## TIER 1 - BLOCKING

**Goal: validate and harden Stage 3C findings. Unblock TIER 2.**

- [ ] **Fix TagFixer to strip album-folder parentheticals** - Stage 3C revealed critical issue: Shaggy track 'It Wasn't Me' had " (International Version)" appended to album field. This suffix caused TWO interrelated failures: (1) The suffix made the track appear to be from a DIFFERENT album than other Shaggy tracks in the same folder, (2) Since it looked like a one-off album track, GetDestDir() routed it to an album subfolder instead of Singles/, (3) LibChecker flagged it as a single-song album subfolder violation. User manually fixed by removing the suffix in MP3TAG, which resolved both issues simultaneously. Root cause: TagFixer must strip common album-folder suffixes (like " (International Version)", " (Deluxe Edition)", etc.) before tagging, preventing this class of false-positive routing and LibChecker errors. The suffix is typically added by the folder name but shouldn't persist in the album tag itself.

- [ ] **Review AudioManager integration workflow and outcomes** - After Stage 3C, review actual execution for AudioManager-specific feedback: What broke? What took longer than expected? What UX pain points emerged in the CLI/routing/tag handling? Record findings to `docs/Development/FEEDBACK-Stage3C-2026-05-XX.md`, then use the checklist in `docs/Historical/WorkflowExecution-2026-04-26/STAGE_5_FEEDBACK_AND_IMPROVEMENT_(BLOCKED).md` (Substeps A-C) to convert feedback into IDEAS.md improvements.

- [ ] **Claude workspace reflection - how did Claude perform, what can improve?** - Post-integration reflection (separate from AudioManager): Analyze Claude's performance and workspace tool effectiveness during Stage 3C. Methodology: Use `/deep-dive` skill to investigate. Check: (1) git history of this session (commits, what was fixed/changed), (2) session history notes in workspace (what worked, what didn't), (3) AudioManager git log for patterns (commit style, scope creep, decision quality), (4) CLAUDE.md vs actual behavior (gaps). Document to `ClaudeOnly/memory/overnight-reflections/2026-05-XX-Stage3C-reflection.md`. Focus on: Claude's decision-making quality, task decomposition accuracy, tool usage patterns, context management efficiency. Identify improvements to: enforced-rules.md (behavior patterns), CLAUDE.md (workspace philosophy), skills, or hooks. Include: what worked well (don't regress), what caused friction, what should change.

- [ ] **Mark historical workflow docs as frozen** - The `docs/Historical/WorkflowExecution-2026-04-26/` folder documents the April 26 workflow planning sessions. These docs are now stale (decisions made, execution pending or in progress). Add a header note to `docs/Historical/WorkflowExecution-2026-04-26/README.md` (create if needed): "This folder is FROZEN as a historical record. Do not edit these docs - they describe pre-execution planning from April 26, 2026. Live workflow feedback and improvements live in IDEAS.md and git history. Kept for reference only." This prevents future attempts to maintain these docs.

---

## TIER 2 - QUALITY

**Goal: improve UX and add test coverage. Start after first clean integration completes.**

- [ ] **Performance investigation - Parser is slow** - User-visible pain point: Parsing 5516 tags took 38.124 seconds (~6.9ms per tag). During runs, user perceives long hangs with no feedback. Profile bottleneck: XML parsing, tag reading, file I/O, or something else? Benchmark against alternatives (streaming vs. loading entire mirror, parallel processing per artist folder, etc.). Once root cause identified, implement optimization. High payback - runs should feel responsive.

- [ ] **Add minimal automated tests for three broad features** - ROI analysis showed full test suite not worth it; these three core features are high-payback.
  - **Motivation:** Each session requires multiple manual dry runs and force regens to verify fixes. Tests for key features catch regressions immediately at build time, enabling faster iteration. Current feedback: each fix session needs 2-3 manual verification cycles (dry run, force regen, spot-check). Tests eliminate this.
  - **Feature 1: Build and launch** - Program compiles cleanly via MSBuild and runs without crashing (e.g. `--help` works). Catches compilation/linkage regressions immediately. Payback: weeks.
  - **Feature 2: Tag normalization** - TagFixer correctly cleans and normalizes tags: TCMP flags set, artist casing preserved (mike. stays lowercase), genre set per rules (Musivation genre for Musivation artists), suffixes stripped (album field cleaned). Payback: 2-3 weeks (catches tag mutation regressions that fail in LibChecker days later).
  - **Feature 3: Routing correctness** - GetDestDir() produces correct destination paths given artist, album, and library state. Sample cases: 3+ songs from album -> album subfolder, 1-2 songs -> Singles, known artist folders -> existing folders, Musivation artists -> Musivation folder. Payback: 1-2 months (wrong routing = real files moved wrong - highest risk).
  - **Scope:** Focus on feature behavior, not individual function testing. Test "artist casing is preserved" not "ExtractAndFixArtists() line 47". Test "Musivation artist gets Musivation folder" not "GetDestDir() branch coverage".
  - **Skipped:** full LibChecker validation (add rules incrementally), full integration test (dry-run covers this), edge case enumeration.
  - Create `AudioManager.Tests` xUnit project; wire into `launch.bat` as "Run tests" menu item; broken tests block exe launch.

- [ ] **TagFixer: extend genre handling for additional artists** - Currently TagFixer sets genre to "Musivation" only for Akira The Don. Expand to:
  - **Loot Bryon Smith** -> Genre = "Musivation" (per Music-Library-Rules.md spec). ALL FILES in the Musivation folder must have the Musivation genre tag.
  - **Generic "Motivation" tracks** -> Genre = "Motivation" (currently not handled)
  - Current implementation: `ShouldFixGenre()` and `DetermineGenre()` in TagFixer.cs need extension. Once implemented, TagFixer will be 100% comprehensive.

- [ ] **UX: Misc routes should batch-review at end instead of per-file** - Auto-accept has been turned off. Still need: collect all Misc-routed files during the run, then present at the end as a single batch review ("These N files would go to Misc - accept all / review one by one / decline all?"). This way Misc routing is visible and auditable without interrupting flow for every file.

- [ ] **Routing proposal UX: split `Proposed:` into human-readable path + filesystem path** - Currently shows one long `Proposed: Musivation\Akira The Don\Singles\...` line. Suggested improvement:
  - `Proposed: Akira The Don / Singles` (short, human-readable)
  - `Path: Musivation\Akira The Don\Singles\Akira The Don;Brian Tracy - UNSTOPPABLE.mp3` (full filesystem path)

- [ ] **Routing proposal UX: `Reason` field should explain WHY, not restate destination** - Currently `Reason` sometimes mirrors what `Proposed:` already shows. Replace restatements with actual decision logic (e.g. "5 songs from album -> album subfolder", "artist folder exists -> auto-route", etc.).

- [ ] **Scan-ahead: show progress indicator during computation** - Symptom: User observed silence after "Scan-ahead: 4 artist(s) will hit 3-song threshold:" with no feedback, thought program hung. Root cause: scan-ahead computation takes several seconds but produces no intermediate output. Fix: print progress during scanning (e.g. "Scanning batch... (checking N artists)" or dot-tick per artist). Improves perceived responsiveness.

- [ ] **Routing proposal UX: concise proposal positioning** - Symptom: The concise proposal summary line (e.g. `-> Artist / Folder`) appears ABOVE the full `Proposed:` filesystem path, making the layout feel out-of-order. User flagged: unclear if line should move, or if display logic should change. Root cause: unclear - needs investigation to determine correct ordering for scannability. Discuss with user before implementing position change.

- [ ] **TagFixer output formatting: blank line between SKIPPED and FIXED entries** - Symptom: TagFixer output has inconsistent spacing - blank lines separate most [FIXED] entries but are missing between [SKIPPED] and following [FIXED] entries. Root cause: output generation doesn't ensure separators after SKIPPED entries. Fix: ensure all [FIXED]/[SKIPPED] blocks are consistently separated by blank lines for readability.

- [ ] **Periodic audit: ensure all artist casing rules are in config** - Artist-name-overrides.xml is the single source of truth. After major TagFixer work sessions, audit codebase (comments, CLAUDE.md, HISTORY.md) for missed rules and migrate to XML. Scott Adams rule was restored 2026-05-05 as a test case - verify no other rules were similarly lost.

- [ ] **Auto-migrate existing Misc songs when scan-ahead promotes an artist** - Currently flagged for MANUAL migration. Revisit with confirmation gate after tests exist.

---

## TIER 3 - POLISH

**Goal: close structural gaps and improve developer experience. Non-blocking.**

- [ ] **Deep dive: audit full library against Music-Library-Rules.md** - scan AudioMirror XMLs, cross-reference every track against the rules doc, produce a violations/gaps report. Then confirm LibChecker catches everything the doc mandates. Goal: a clean LibChecker run means full conformance.
  - *Partial progress (2026-04-09)*: rules gap analysis done. Added `CheckAlbumSubfolderRule()` and `CheckGenreVsFolder()`. Remaining: run LibChecker on the full library, scan AudioMirror XMLs for violations not caught by LibChecker.

- [ ] **Centralise rules - one system for both integration routing and LibChecker** - currently routing rules (artist -> folder mapping, genre overrides, special cases like ATD) and LibChecker validation rules live in separate places. Refactor into a single `RulesEngine` concept: rules defined once, consumed by both MusicIntegrator (routing decisions) and LibChecker (validation). Benefits: add one rule, both systems honour it; no risk of the two diverging. Also: audit for missing ATD (Akira The Don) rules - suspected gap between what TagFixer sets and what LibChecker checks.
  - **Structural fix for tag/validation sync failures:** TagFixer and LibChecker currently have independent, unsynchronised rule sets. This caused a real failure: TagFixer SKIPPED a file ("no fixes needed") but LibChecker later flagged it - because each component only knows its own rules. A shared RulesEngine makes this class of failure structurally impossible: both components consult the same rules, so a file that passes TagFixer provably passes LibChecker.

- [ ] **Simplify launch.bat - move menu logic into Program.cs** - RivalsVidMaker's `run.bat` is 2 lines; all menu/mode logic lives in Python. AudioManager's `launch.bat` is 91 lines with its own menu, duplicating CLI arg logic already in `Program.cs`. Refactor: `launch.bat` becomes a ~5-line wrapper that just builds + runs `AudioManager.exe` with no args; all menu logic lives in Program.cs where it's testable and debuggable.

- [ ] **Sources/ routing not implemented in GetDestDir()** - `Constants.SourcesDir` exists but `MusicIntegrator.GetDestDir()` has no routing logic for Sources/Films, Sources/Shows, or Sources/Anime. Films/Shows/Anime tracks currently fall to Misc and require manual folder-picker redirection. `Music-Library-Rules.md` documents the expected routing rules (Films subfolder = film name, Shows subfolder = show name, Anime = separate).
  - **Challenge:** Automating this is difficult - metadata alone rarely indicates source type. Current approach: if `Album` contains "OST" or "Soundtrack", prompt user for subfolder choice rather than defaulting to Misc.
  - **Alternative approach:** Study existing Sources/Films, Sources/Shows, Sources/Anime tracks for metadata patterns (album names, artist names, genre, etc.) that distinguish them. May reveal rough heuristics for auto-detection, or may confirm that folder-picker prompt is the only reliable option.
  - **Recommendation:** TIER 4 exploratory task - audit existing metadata patterns before deciding if automation is feasible. May need to accept manual folder-picker as permanent solution.

- [ ] **Audit libchecker-exceptions.xml - distinguish bug fixes from genuine exceptions** - review all exceptions to identify which are workarounds for LibChecker regex bugs (e.g., "version" in legitimate titles like Alan Watts tracks) vs. genuine tracks that need exemption (e.g., Eric Thomas bonus content). For each regex-bug workaround, ensure there's a corresponding TIER 1 code improvement in IDEAS. Goal: exceptions.xml contains only track-specific needs, not rule improvements. Low priority.

- [ ] **AudioMirrorCommitter safety check - prevent commit on ANY issues, not just LibChecker** - currently skips auto-commit only if LibChecker reported hits, but doesn't account for other issues like unexpected file extensions (discovered during Reflector mirror phase). Update check: if Reflector found unexpected extensions OR LibChecker reported hits, skip commit and notify user. Goal: auto-commit only runs when entire pipeline is completely clean.

- [ ] **Re-enable AudioMirrorCommitter auto-commit** - currently disabled (shows manual instructions instead). Re-enable when program is more mature and proven stable on real library operations. Prerequisites: (1) TIER 1 all verified, (2) several weeks of stable runs with zero accidental data loss, (3) both safety checks above in place. Low priority - manual commits are safe and auditable.
  - **Note:** Old auto-commit logic is commented out in `AudioMirrorCommitter.TryCommit()` (lines ~60-85) for easy re-enable.

- [ ] **Fix report table formatting - markdown tables instead of plain text** - Symptom: Current reports (`reports/2026/YYYY-MM-DD - AudioReport.md`) render statistics tables as plain text (raw columns, no markdown formatting). Root cause: `ReportWriter.cs` (and/or `Analyser.cs`) emits plain text, not markdown. Impact: tables don't render nicely on GitHub, harder to scan. Fix: modify output to emit proper markdown tables with pipe-delimited columns and header separators (e.g. `| Header | Header |` + `|--------|--------|`). Improves readability and GitHub rendering consistency.

- [ ] **Low priority: Evaluate removing .md integration logs in favour of decision XMLs** - Integration logs (`logs/integration-*.md`) and decision XMLs overlap in content. If XMLs contain all routing information, the .md logs may be redundant. Evaluate what .md logs have that XMLs don't (human-readable narrative vs structured data). If XMLs are sufficient for audit and analysis, remove .md logs to simplify the output.

- [ ] **Low priority idea: Routing decision analysis mode** - Add a mode that reads decision XMLs, cross-references routing decisions against routing rules code and LibChecker rules, and flags inconsistencies. Produces a report: "these N files were routed to X but LibChecker would flag them as Y". Pairs well with the "Centralise rules" refactor. Exploratory - assess value after the first real integration run produces decision XML data to analyse.

---

## TIER 4 - FUTURE

**Goal: exploratory features and advanced enhancements, tackled after core tiers are stable.**

- **LibChecker & routing: analyze library patterns to decide automatically instead of prompting user** - After several stable integration runs with mature rules, explore building statistical models of library structure (artist folder distribution, album patterns, file counts) to auto-decide routing instead of user confirmation. Example: if artist consistently has 1-2 song albums, auto-route to Singles; if genre appears 95% of the time for an artist, use as default. Lower priority now since current rules are solid and catch issues well (Stage 3C validation confirmed). Revisit after TIERS 1-3 are complete and you have more decision XML history to analyze.

- **"My Edits" tracking** - detect locally edited songs by comparing duration to official track (>3-4s diff = protected from overwrite).
- **Parody/original song pairing detection** - flag songs where a parody and its original are both in the library.
- **Album completion detection** - cross-reference library against Spotify/MusicBrainz; flag where 50%+ of an album is owned.
- **Fuzzy artist name matching** - handle artist name variations during routing ("The Beatles" vs "Beatles", featured artist formatting differences). Only matters at scale.
- **Fuzzy duplicate title matching** - extend the pre-integration duplicate check to catch near-matches (e.g. "Song (feat. X)" vs "Song", "Song - Remix" vs "Song"). Approach: normalise both sides before comparison by stripping featured artist parentheticals, stripping remix/edit/version suffixes, collapsing whitespace. Blocked by: the exact-match duplicate check must be in place first.

---

## Parked / Deprioritized

- **Auto-routing for known routing cases** - When a routing decision is certain (perfect match for a known case), skip the human confirmation and route automatically. **EXPLICITLY GATED:** Do not implement until David says "I'm confident enough in the program." David's words: "WAIT until I explicitly say I'm confident enough." Only revisit after several real integration runs with zero incorrect routings and TIER 2 test coverage in place.

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

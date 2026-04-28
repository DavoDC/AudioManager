# Ideas & Future Work

Single source of truth for all pending work. Settled decisions and completed features -> `HISTORY.md`.

## Organization: Tiered Priorities

Work is grouped by safety tier and milestone. Items within a tier can be done in any order or in parallel.
**Do NOT move to the next tier until the current tier is verified working on real data.**

**Why tiers?** AudioManager moves real files. Safety is non-negotiable. Tiers make blocking items explicit. TIER 0 and TIER 1 together form the "First Real Integration" milestone - all items must be complete before doing the real integration run. TIER 0 ensures safety (no data corruption), TIER 1 ensures we capture routing data and can validate the run worked. Then TIER 2+ for quality/polish. This matches how a solo developer actually reasons about work.

---

# MILESTONE: First Real Integration Run

**Combined TIER 0 + TIER 1 prerequisites.** All items below must be complete before doing Stage 3C integration.
- TIER 0 ensures safety (no corruption, all validations in place)
- TIER 1 ensures decision logging (routing decisions captured, patterns extractable, audit trail preserved)
- Success = first real integration runs clean, all decisions logged, at least 3 routing patterns extracted

---

## TIER 0 - BLOCKING (Safety Prerequisites)

**Goal: ensure integration cannot corrupt the library.** These items must be in place before any real integration run.


- [ ] **BUG: Console.Clear() wipes routing decisions - user can't see/audit what's routing where** - Dry-run 2026-04-28 showed integration proceeding but routing decisions vanished from terminal as soon as they were printed (line 251 calls Console.Clear()). This blocks user from auditing the routing and confirming decisions. Root cause: line 251 `Console.Clear()` clears the output after showing each track's routing decision. Solution: (1) Remove Console.Clear() calls during routing (lines 251, 369, 389); (2) Instead, use blank lines (Console.WriteLine()) to separate sections; (3) Add timestamps to show when each decision is logged (see item below). Result: user can scroll back and see all routing decisions that were made.

- [ ] **UX: Add timestamped log entries during routing** - Dry-run 2026-04-28 showed routing decisions being made but user couldn't see progress timing - no indication of how fast files were being processed. Add `Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Processing: {track.Artists} - {track.Title}")` at key points: (1) before tag fixing starts, (2) after each track's tags are fixed, (3) after each routing decision is logged. This gives visibility into progress and helps debug slow operations. Wire into TeeWriter so timestamps appear in both console and log file.

- [ ] **UX: Add confirmation gates between every logical section** - Dry-run shows integration running but user never gets to confirm before major operations. Requirement: (1) After "Pre-integration validation passed", pause and show "Proceed? (Y/N)" prompt; (2) After routing completes, pause before "Integration complete" so user can review decisions. Implementation: add ReadLine() confirmation gates in Program.cs at major decision points. User must explicitly type Y (or other option) to continue. This makes dry-run auditable and gives user control over when integration proceeds.

- [ ] **UNBLOCKED: Real integration run - now ready after P0 fixes** - P0 bugs fixed (2026-04-28): "startIndex" substring bounds-checking (DecisionLog.cs) + fail-fast error handling. Dry-run completed successfully on full NewMusic batch. BUT: UX issues must be fixed first before real integration:
  - [ ] **BLOCKING:** Remove Console.Clear() and add timestamped logging (see items above) so user can audit routing decisions
  - [ ] **BLOCKING:** Add confirmation gates so user can control when integration proceeds
  - **Once UX issues fixed,** sequence for real integration run:
    1. **Tag cleaning:** Run `AudioManager tagfix --dry-run` to preview tag cleanup
    2. **Tag fix (real):** Run `AudioManager tagfix` to clean NewMusic files
    3. **Integration dry-run:** Run `AudioManager integrate --dry-run` to preview routing with timestamps + confirmations
    4. **Integration (real):** Run `AudioManager integrate` to move files (after user approves)
    5. **Post-validation:** LibChecker auto-runs; verify CLEAN
    6. **Commit:** If CLEAN, commit AudioMirror changes to git





---

## TIER 1 - MVP (Core Pipeline Works)

**Goal: prove the completed integration pipeline works on real data.** All features are built; this is the validation phase.




- [ ] **Formalize album vs single preference rule in routing** - Implement the album-preference rule from STAGE_3B as part of duplicate detection: "If singles exist in library + full album version is available, DELETE singles and KEEP ALBUM." Currently integrator offers user choice per duplicate. Expand to: (1) Detect when new file is part of an album (has album metadata + multiple tracks from same album in NewMusic). (2) When duplicate found: check if old version is a single AND new version is from an album. (3) If yes, recommend [L] (delete from library, keep album) as the default choice. (4) Log this decision pattern to DecisionLog so we can extract "album preference pattern" as a routing insight. **Why:** album versions are musically superior to singles (same tracks, album context), so library should prefer albums. Rule discovered through manual review (54.5% of incoming tracks were rejected singles).

**Note:** TagFixer requirement already specified in TIER 0 "CRITICAL DESIGN: Separate tag fixing from integration". Integration pipeline sequence: NewMusic → TagFixer (clean tags, auto-delete instrumentals) → Integrator (route files, handle duplicates with [L] option) → Analyzer (report). All prerequisites defined in TIER 0.

---

## TIER 2 - QUALITY (Robustness & UX Polish)

**Goal: improve user experience for real integration (visibility, readability, control) and add minimal test coverage for high-risk code paths.**

**PRIORITY: Fix before real integration run**

- [ ] **UX: Shorten file paths in duplicate dialogs** - When duplicate detection dialog is shown, displays full absolute paths (e.g., `C:\Users\David\GitHubRepos\AudioMirror\AUDIO_MIRROR\Musivation\Akira The Don\MEANINGWAVE MASTERPIECES V\Akira The Don;Rupert Spira - INTO THE INFINITE.xml`). These are impossible to compare at a glance. Improve readability by showing relative paths from library root. Example: show `Musivation\Akira The Don\MEANINGWAVE MASTERPIECES V\...` instead of full C:\Users\... path. Implementation: in duplicate dialogs and error messages, extract and display only the portion after `AUDIO_MIRROR\` or `Downloads\NewMusic\` prefix. Affects: MusicIntegrator.cs duplicate dialog, DeriveLibraryPathFromMirrorPath output.

- [ ] **UX: Add blank lines between tag fixing output blocks** - TagFixer output shows "WOULD FIX" blocks back-to-back with no visual separation, making it hard to scan. Add blank line (Console.WriteLine()) between each track's fixes. Makes dry-run output more readable.


- [ ] **TagFixer: extend genre handling for additional artists** - Currently TagFixer sets genre to "Musivation" only for Akira The Don. Expand to:
  - **Loot Bryon Smith** → Genre = "Musivation" (per Music-Library-Rules.md spec)
  - **Generic "Motivation" tracks** → Genre = "Motivation" (currently not handled)
  - Current implementation: `ShouldFixGenre()` and `DetermineGenre()` in TagFixer.cs need extension to check artist name and/or existing genre tags. Once implemented, TagFixer will be 100% comprehensive.

- [ ] **Performance investigation - Parser is slow** - Analysis runs take time; Parser phase is noticeably slow when processing 5000+ MP3s. Profile the bottleneck: is it XML parsing, tag reading, file I/O, or something else? Benchmark against alternative approaches (e.g., streaming vs. loading entire mirror into memory, parallel processing per artist folder, etc.). Document findings and propose optimization target for TIER 2 implementation (if payback is clear).

- [ ] **Add minimal automated tests (scoped down from kitchen-sink)** - ROI analysis showed full test suite not worth it; these three are.
  - **Smoke build test** (~50 lines): MSBuild builds cleanly; exe runs with `--help` without crashing. Catches today's class of bug. Payback in weeks.
  - **`GetDestDir()` routing tests** (~150 lines): fixed inputs (artist, album, scan-ahead set) -> fixed destinations. Routing is the most dangerous code path - wrong dest = real files moved wrong. Payback in 1-2 months.
  - **`PreProcessTags()` tag-mutation tests** (~80 lines): no-TCMP input -> TCMP=True; Akira The Don wrong-genre input -> Musivation. Tiny and easy.
  - **Skipped:** full LibChecker-rule suite (~400+ lines, 6-12 month payback - add incrementally when rules change, don't backfill). Full integration test with fake MP3s (too heavy, dry-run already covers).
  - Add `AudioManager.Tests` xUnit project; wire into `launch.bat` as "Run tests" menu item; broken tests block exe launch.

---

## TIER 3 - POLISH (Structural Alignment & Nice-to-Have)

**Goal: close structural gaps and improve developer experience.** Non-blocking enhancements.

- [ ] **Deep dive: audit full library against Music-Library-Rules.md** - scan AudioMirror XMLs, cross-reference every track against the rules doc, produce a violations/gaps report. Then confirm LibChecker catches everything the doc mandates. Goal: a clean LibChecker run means full conformance.
  - *Partial progress (2026-04-09)*: rules gap analysis done. Added `CheckAlbumSubfolderRule()` and `CheckGenreVsFolder()`. Remaining: run LibChecker on the full library, scan AudioMirror XMLs for violations not caught by LibChecker.

- [ ] **Simplify launch.bat - move menu logic into Program.cs** - RivalsVidMaker's `run.bat` is 2 lines; all menu/mode logic lives in Python. AudioManager's `launch.bat` is 91 lines with its own menu, duplicating CLI arg logic already in `Program.cs`. Refactor: `launch.bat` becomes a ~5-line wrapper that just builds + runs `AudioManager.exe` with no args; all menu logic lives in Program.cs where it's testable and debuggable.
- [ ] **Auto-migrate existing Misc songs when scan-ahead promotes an artist** - currently flagged for MANUAL migration (deemed too risky to auto-move existing library files). Revisit with a confirmation gate after tests exist.
- [ ] **Sources/ routing not implemented in GetDestDir()** - `Constants.SourcesDir` exists but `MusicIntegrator.GetDestDir()` has no routing logic for Sources/Films, Sources/Shows, or Sources/Anime. Films/Shows/Anime tracks currently fall to Misc and require manual folder-picker redirection. `Music-Library-Rules.md` documents the expected routing rules (Films subfolder = film name, Shows subfolder = show name, Anime = separate). 
  - **Challenge:** Automating this is difficult - metadata alone rarely indicates source type. Current approach: if `Album` contains "OST" or "Soundtrack", prompt user for subfolder choice rather than defaulting to Misc.
  - **Alternative approach:** Study existing Sources/Films, Sources/Shows, Sources/Anime tracks for metadata patterns (album names, artist names, genre, etc.) that distinguish them. May reveal rough heuristics for auto-detection, or may confirm that folder-picker prompt is the only reliable option.
  - **Recommendation:** TIER 4 exploratory task - audit existing metadata patterns before deciding if automation is feasible. May need to accept manual folder-picker as permanent solution.
- [ ] **Audit libchecker-exceptions.xml - distinguish bug fixes from genuine exceptions** - review all exceptions to identify which are workarounds for LibChecker regex bugs (e.g., "version" in legitimate titles like Alan Watts tracks) vs. genuine tracks that need exemption (e.g., Eric Thomas bonus content). For each regex-bug workaround, ensure there's a corresponding TIER 1 code improvement in IDEAS. Goal: exceptions.xml contains only track-specific needs, not rule improvements. Low priority - documents intent rather than changing behaviour.
- [ ] **AudioMirrorCommitter safety check - prevent commit on ANY issues, not just LibChecker** - currently skips auto-commit only if LibChecker reported hits, but doesn't account for other issues like unexpected file extensions (discovered during Reflector mirror phase). Update check: if Reflector found unexpected extensions OR LibChecker reported hits, skip commit and notify user. Goal: auto-commit only runs when entire pipeline is completely clean.
- [ ] **Re-enable AudioMirrorCommitter auto-commit** - currently disabled (shows manual instructions instead). Re-enable when program is more mature and proven stable on real library operations. Auto-commit was too risky during active development. Prerequisites: (1) TIER 1 all verified, (2) several weeks of stable runs with zero accidental data loss, (3) both safety checks above in place. Low priority - manual commits are safe and auditable.
  - **Note:** Old auto-commit logic is commented out in `AudioMirrorCommitter.TryCommit()` (lines ~60-85) for easy re-enable. Uncomment when prerequisites met.

- [ ] **Fix report table formatting - markdown tables instead of plain text** - Current reports (`reports/2026/YYYY-MM-DD - AudioReport.md`) render statistics tables as plain text with no formatting. Example: "% Decade Occurrences" section shows numbered rows without table structure. Modify `ReportWriter.cs` (and/or `Analyser.cs` if it generates the data) to emit proper markdown tables with pipe-delimited columns and header separators. Affects readability and consistency with GitHub markdown rendering. **Why:** Reports are committed to AudioMirror repo and viewed on GitHub; plain text looks unprofessional, markdown tables are readable and well-formatted.

---

## TIER 4 - FUTURE (Lower Priority / Nice-to-Have)

**Goal: exploratory features and advanced enhancements, tackled after core tiers are stable.**

- **"My Edits" tracking** - detect locally edited songs by comparing duration to official track (>3-4s diff = protected from overwrite).
- **Parody/original song pairing detection** - flag songs where a parody and its original are both in the library.
- **Album completion detection** - cross-reference library against Spotify/MusicBrainz; flag where 50%+ of an album is owned.
- **Fuzzy artist name matching** - handle artist name variations during routing ("The Beatles" vs "Beatles", featured artist formatting differences). Only matters at scale.
- **Fuzzy duplicate title matching** - extend the pre-integration duplicate check to catch near-matches (e.g. "Song (feat. X)" vs "Song", "Song - Remix" vs "Song"). Approach: normalise both sides before comparison by stripping featured artist parentheticals, stripping remix/edit/version suffixes, collapsing whitespace. Blocked by: the exact-match duplicate check (TIER 0) must be in place first.

---

## Parked / Deprioritized

- **DECISION GATE: Python Rewrite vs .NET 8 Migration** - AudioManager is .NET Framework; could migrate to .NET 8 SDK-style (lower cost, same language, taglib#-compatible) or rewrite in Python (no build step, lightweight deps via `mutagen`). **Status:** Parked. Program works well; no immediate need to decide. Revisit only if .NET becomes a blocker or if Python advantages outweigh rewrite cost. Decision factors: token cost estimate, confirm Python libs cover TagLib# use cases, ensure file I/O scope is matched.

- **Review mode - library pruning / song-by-song decision tracking** - Add a new `review` mode that walks every song one by one, shows context (tags, folder, optional play count / popularity / lyrics), and asks: keep / remove / defer. Every decision is persisted to a config file (e.g. `config/review-decisions.xml` or similar) with: song fingerprint (artist + title or file hash), decision, date, reason. Old decisions are re-surfaced periodically (e.g. after 12 months) so the review is not one-shot - tastes change, a "keep" today may be a "remove" next year.
  - **Removal candidate signals** (surfaced in review mode to guide decisions, not auto-removed):
    - **Low play count** - cross-reference against iTunes, Last.fm, and/or Spotify listening history. A song never played in 2+ years is a prime candidate.
    - **Negative lyrical/emotional tone** - screen lyrics (via a lyrics API or local cache) and flag songs with heavily negative/depressive/aggressive content on the theory that repeated subconscious exposure shapes mood. This is subjective and must stay advisory, not automated.
  - **Auditable, reversible:** decisions live in a committed config file; a "remove" decision is the review-mode verdict, not an immediate file delete. Actual removal is a separate explicit step after review is complete, with dry-run first.
  - **Integrates with existing infra:** can read from AudioMirror XML (existing scan target), reuse LibChecker exception pattern (external XML config), reuse dry-run pattern from MusicIntegrator.
  - **Status:** Deprioritized. NewMusic decision logging (TIER 1) is higher priority. Revisit after core integration pipeline is stable and you have operational experience with large libraries. Currently: if library needs pruning, do it ad-hoc or manually via existing tools.

---

## See Also

- `docs/HISTORY.md` - completed features, settled design decisions, parked ideas
- `docs/Music-Library-Rules.md` - canonical rules for library structure
- `docs/NewMusic-Integration-Plan-20260308.md` - past batch integration (March 2026 batch A)
- `docs/NewMusic-Integration-Plan-20260407.md` - past batch integration (April 2026)
- `docs/AudioMirror-Format.md` - AudioMirror XML format and repo info

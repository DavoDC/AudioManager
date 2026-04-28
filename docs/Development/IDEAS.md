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

- [ ] **CRITICAL: First real integration run (after TagFixer prep)** - Prerequisites met: TagFixer module now available. Sequence:
  1. **Tag cleaning:** Run `AudioManager tagfix --dry-run` to preview tag cleanup (removes parentheticals, fixes featured artists, renames files)
  2. **Tag fix (real):** Run `AudioManager tagfix` to clean NewMusic files
  3. **Integration dry-run:** Run `AudioManager integrate --dry-run` to preview routing decisions into library
  4. **Integration (real):** Run `AudioManager integrate` to move files (note: THIS SESSION only do dry-run and analysis, no real integrate)
  5. **Post-validation:** LibChecker auto-runs after integration; verify it reports CLEAN
  6. **Commit:** If CLEAN, commit AudioMirror changes to git
  - **NewMusic folder contains ~40 songs across multiple artists/albums - good sample for validation.**
  - **This is the first empirical proof that the integration pipeline works on real data. Once complete, move this item to HISTORY and proceed to TIER 1.**

- [ ] **CRITICAL DESIGN: Separate tag fixing from integration (integration = routing only)** - The program must have clear separation of concerns: (1) **Tag Fixing** happens AUTOMATICALLY as a pre-integration step (during analysis or before integration starts), and (2) **Integration** purely routes files to destination folders based on clean tags. Integration should NEVER touch tags - it assumes all input files are already cleaned. This keeps integration simple, safe, and testable. Currently PreProcessTags() in MusicIntegrator.cs only sets TCMP=1 and Musivation genre, but it should NOT be expanded with full tag cleanup. Instead, create a separate **TagFixer** module that: (1) reads raw MP3s from Downloads/NewMusic, (2) applies all tag cleanup rules (remove unwanted words, ensure featured artists in TPE1, rename files per naming convention), (3) reports what was fixed, (4) saves back to disk. Then Integration reads the cleaned files and routes them. **Why:** integration failures should never be "tags were wrong", only "routing decision was wrong". Separate tool = separate responsibility = easier to test and debug.


---

## TIER 1 - MVP (Core Pipeline Works)

**Goal: prove the completed integration pipeline works on real data.** All features are built; this is the validation phase.

- [ ] **Integrator: Auto-log routing decisions to XML** (PREREQUISITE FOR FIRST REAL INTEGRATION) - During integration (Stage 3C), MusicIntegrator should write each routing decision to `decisions.xml` as it processes files. Schema: artist, album, track, sourceFile, destinationPath, routingReason, timestamp. **Why valuable:** (1) **Knowledge extraction** - corpus reveals which routing rules fired for which tracks, patterns in folder placement (artist auto-route, Musivation rule, manual folder-picker choice), edge cases and hard-to-classify tracks. Extract 3+ actionable patterns (e.g. "Akira The Don → Musivation 100% of time", "album versions always preferred over singles", "manual folder-picker used for X% of files"). Use patterns to improve future routing heuristics. (2) **Audit trail** - complete log of what the integrator did, where it put files, and why. Answers "why did this song go to that folder?" and enables rollback/verification if integration goes wrong. **Critical:** This must be implemented BEFORE the first real integration run - once files are integrated, that routing data is lost forever. (3) **Implementation:** Add `DecisionLog` class to log decisions; integrate into `MusicIntegrator.GetDestDir()` and `FolderPickerHandler` so every routing decision is captured. Store as `docs/Historical/WorkflowExecution-YYYY-MM-DD/decisions.xml`. **Success:** Integrator logs all decisions automatically; at least 3 patterns extracted from corpus post-integration; audit trail enables verification. Implement and test with dry-run BEFORE first real integration (April 28 session).

**Note:** TagFixer requirement already specified in TIER 0 "CRITICAL DESIGN: Separate tag fixing from integration". Integration pipeline sequence: NewMusic → TagFixer (clean tags) → Integrator (route files) → Analyzer (report). All prerequisites defined in TIER 0.

---

## TIER 2 - QUALITY (Robustness & Test Coverage)

**Goal: eliminate the whole class of build-break bug that cost us Phase 0 time, and pin down the highest-risk code paths. Also investigate performance bottlenecks.**

- [ ] **Performance investigation - Parser is slow** - Analysis runs take time; Parser phase is noticeably slow when processing 5000+ MP3s. Profile the bottleneck: is it XML parsing, tag reading, file I/O, or something else? Benchmark against alternative approaches (e.g., streaming vs. loading entire mirror into memory, parallel processing per artist folder, etc.). Document findings and propose optimization target for TIER 2 implementation (if payback is clear).

### DECISION GATE: Python Rewrite vs .NET 8 Migration

**Status:** Pending evaluation (David's strategic call)

**The Fork:** Current plan (Option A) surveys .NET 8 migration blockers, then migrates to SDK-style csproj. Alternative (Option B): rewrite the console app in Python instead of migrating .NET.

## Why Python is Being Considered

- **No build step** - Python script runs directly, no compilation dependency
- **No VS2022 dev dependency** - lighter development environment
- **Lightweight dependencies** - modern Python audio metadata libs (`mutagen`, `tinytag`) vs .NET/TagLib#
- **ROI question** - which approach costs fewer tokens and yields better dev experience?

**Why this matters:** David prefers lightweight languages and no-build tooling. C# and C++ aren't readable during Claude sessions. Python would be simpler to maintain and extend.

## Decision Criteria

1. **Token cost:** Opus planning + Haiku execution for Python rewrite vs straight .NET 8 migration
2. **Dependency check:** confirm Python has audio metadata equivalent (expect `mutagen` to cover TagLib# use cases)
3. **Scope match:** ensure Python can handle the integration/analysis workload (it can - it's not CPU-bound, just file I/O and XML generation)

## Next Steps (for David)

1. Decide whether to evaluate Python rewrite (2-3 hour planning + evaluation)
2. If yes: `Opus` plans Python approach, `Haiku` implements it
3. If no: proceed with TIER 2 .NET migration as written

**DO NOT start TIER 2 until this decision is made.**

---

**If Python:** TIER 2 becomes Python rewrite + tests. **If .NET:** proceed with items below as written.

---

- [ ] **Survey: confirm .NET 8 migration has no blockers** - grep for `ConfigurationManager`, `AppDomain`, `System.Web`, `System.ServiceModel`, `Remoting`. Check `App.config` contents. Confirm TagLib# is the only NuGet dep. Expect no blockers - this is a console app.
- [ ] **Migrate project to .NET 8 (SDK-style csproj)** - replace old-style csproj with ~15-line SDK-style (`<Project Sdk="Microsoft.NET.Sdk">` + `TargetFramework` + `PackageReference`). Delete `packages/` folder + `packages.config`. Delete or trim `Properties/AssemblyInfo.cs`. Update `launch.bat` exe path (`bin\Release\net8.0\AudioManager.exe`). Claude to test-build both modes himself before committing.
  - **Why:** SDK-style csproj auto-includes all `.cs` files. The `AudioMirrorCommitter.cs` bug that broke Phase 0 cannot happen in SDK-style. Also: faster builds, no `packages/` folder, PackageReference instead of packages.config, LTS until Nov 2026.
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

---

## TIER 4 - FUTURE (Lower Priority / Nice-to-Have)

**Goal: exploratory features and advanced enhancements, tackled after core tiers are stable.**

- **TagFixer: extend genre handling for additional artists** - Currently TagFixer sets genre to "Musivation" only for Akira The Don. Expand to:
  - **Loot Bryon Smith** → Genre = "Musivation" (per Music-Library-Rules.md spec)
  - **Generic "Motivation" tracks** → Genre = "Motivation" (currently not handled)
  - Current implementation: `ShouldFixGenre()` and `DetermineGenre()` in TagFixer.cs need extension to check artist name and/or existing genre tags. Low priority - for now, manually set genre in MP3Tag if you have Loot Bryon Smith or other Motivation-tagged tracks before running tagfix. Once implemented, TagFixer will be 100% comprehensive.
- **"My Edits" tracking** - detect locally edited songs by comparing duration to official track (>3-4s diff = protected from overwrite).
- **Parody/original song pairing detection** - flag songs where a parody and its original are both in the library.
- **Album completion detection** - cross-reference library against Spotify/MusicBrainz; flag where 50%+ of an album is owned.
- **Fuzzy artist name matching** - handle artist name variations during routing ("The Beatles" vs "Beatles", featured artist formatting differences). Only matters at scale.
- **Fuzzy duplicate title matching** - extend the pre-integration duplicate check to catch near-matches (e.g. "Song (feat. X)" vs "Song", "Song - Remix" vs "Song"). Approach: normalise both sides before comparison by stripping featured artist parentheticals, stripping remix/edit/version suffixes, collapsing whitespace. Blocked by: the exact-match duplicate check (TIER 0) must be in place first.

---

## Parked / Deprioritized

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

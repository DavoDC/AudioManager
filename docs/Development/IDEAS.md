# Ideas & Future Work

Single source of truth for all pending work. Settled decisions and completed features -> `HISTORY.md`.

## Organization: Tiered Priorities

Work is grouped by safety tier. Items within a tier can be done in any order or in parallel.
**Do NOT move to the next tier until the current tier is verified working on real data.**

**Why tiers?** AudioManager moves real files. Safety is non-negotiable. Tiers make blocking items explicit (TIER 0: safety prerequisites), then MVP (does it work?), then quality/polish (nice-to-have). This matches how a solo developer actually reasons about work.

---

## TIER 0 - BLOCKING (Safety Prerequisites)

**Goal: ensure integration cannot corrupt the library.** These items must be in place before any real integration run.

- [ ] **CRITICAL: Commit AudioMirror changes, then run real integration with NewMusic batch** - AudioMirror is currently out of sync (98 file changes pending). (1) Commit the mirror changes to git in AudioMirror repo. (2) Run `AudioManager integrate --dry-run` to preview all routing decisions. (3) If routing looks correct, run `AudioManager integrate` (no dry-run) to move files into the library. (4) Post-integration validation will auto-run LibChecker - verify it reports CLEAN. This is the first empirical proof that the integration pipeline works on real data. **NewMusic folder contains ~40 songs across multiple artists/albums - good sample for validation.** Once complete, move this item to HISTORY and proceed to TIER 1.

- [ ] **CRITICAL DESIGN: Separate tag fixing from integration (integration = routing only)** - The program must have clear separation of concerns: (1) **Tag Fixing** happens AUTOMATICALLY as a pre-integration step (during analysis or before integration starts), and (2) **Integration** purely routes files to destination folders based on clean tags. Integration should NEVER touch tags - it assumes all input files are already cleaned. This keeps integration simple, safe, and testable. Currently PreProcessTags() in MusicIntegrator.cs only sets TCMP=1 and Musivation genre, but it should NOT be expanded with full tag cleanup. Instead, create a separate **TagFixer** module that: (1) reads raw MP3s from Downloads/NewMusic, (2) applies all tag cleanup rules (remove unwanted words, ensure featured artists in TPE1, rename files per naming convention), (3) reports what was fixed, (4) saves back to disk. Then Integration reads the cleaned files and routes them. **Why:** integration failures should never be "tags were wrong", only "routing decision was wrong". Separate tool = separate responsibility = easier to test and debug.

- [ ] **HIGHEST PRIORITY (TIER 0 BLOCKER): Auto tag fixer before integration** - Create a new **TagFixer** module/command that runs BEFORE integration. Purpose: clean raw NewMusic files so integration only has to route, never fix. Cleanup rules (per Music-Library-Rules.md): 
  - (1) **Remove entire parenthetical phrases from Title/Album tags** (NOT just substrings):
    - `"Cool Song (feat. Akon)"` → `"Cool Song"` (remove the ENTIRE `(feat. Akon)` phrase)
    - `"Track (Album Version)"` → `"Track"` (remove entire phrase, not just the word "Version")
    - `"Song (Explicit)"` → `"Song"`, `"Track (Radio Edit)"` → `"Track"`, `"Title (Original)"` → `"Title"`, `"Name (Remix)"` → `"Name"`
    - Phrase list: `(feat. ...)`, `(ft. ...)`, `(Album Version)`, `(Explicit)`, `(Edit)`, `(Radio Edit)`, `(Original)`, `(Remix)`, `(Version)` - match and strip the ENTIRE parenthetical section
    - **CRITICAL:** Do NOT strip just the word "feat." or "ft." - they may be part of other words. Strip the full `(feat. X)` or `(ft. X)` parenthetical with featured artist name inside.
    - BUT preserve legitimate "feat." info by moving featured artists to TPE1 artist tag (see step 3)
  - (2) Rename files to match convention: `{all-semicolon-separated-artists} - {title}.mp3`. Example: filename `"Chiddy Bang - Mind Your Manners (feat. Icona Pop).mp3"` becomes `"Chiddy Bang;Icona Pop - Mind Your Manners.mp3"` (featured artist moved to beginning, filename cleaned)
  - (3) **Ensure TPE1 (artist tag) has featured artists semicolon-separated, primary first.** Extract artist names from removed parentheticals and add to TPE1. Example: if title was `"Cool Song (feat. Akon)"`, add Akon to TPE1 as `"MainArtist;Akon"`.
  - (4) Set TCMP=1 on all tracks.
  - (5) Set genre for Musivation/Motivation tracks if not already set.
  - (6) Report per-file: what was fixed, any skips, any errors.
  - This is the BLOCKING item for running real integration - if TagFixer doesn't exist, you must manually clean NewMusic files in MP3Tag first. Once TagFixer exists, integration pipeline is fully automated: user runs `TagFixer` → `Integrator` → `Analyzer`, all in sequence, with zero manual tag work.

---

## TIER 1 - MVP (Core Pipeline Works)

**Goal: prove the completed integration pipeline works on real data.** All features are built; this is the validation phase.

- [ ] **Fix LibChecker Sources OST validation - smart folder-to-album matching** - currently requires ALL tracks in Sources/ to have "OST" in album tag, but this is wrong. Official soundtracks should have "OST", but featured tracks (e.g., a-ha's "Hunting High and Low" featured in Super Mario Bros) should keep original album names. New rule: If album contains the source folder name, require "OST" at end. Otherwise, no OST requirement. Example: `Sources/Shows/Peacemaker/` + album "Peacemaker OST" ✓, but album "We Are the Ones" ✗. Fixes 7 false positive flags.
- [ ] **TagFixer must be called BEFORE integration** - (replaces old "Integrator: auto-apply tag cleanup during integration" item) Integration assumes clean tags; TagFixer handles all tag work. Calling sequence: user drops batch in Downloads/NewMusic → runs TagFixer → runs Integrator → runs Analyzer. TagFixer output feeds directly into Integrator input. Once TagFixer is implemented, the entire pipeline is automated. **CRITICAL DESIGN:** Detection rules (what LibChecker flags) ≠ Removal rules (what TagFixer removes). Example: LibChecker flags "feat." anywhere in title (to catch accidental inclusions), but TagFixer must be careful not to remove all "feat." - some are legitimate featuring credits, some are part of words (false positives). Cleanup patterns must be explicitly curated, never blindly auto-remove based on LibChecker hits. When in doubt, flag for human review rather than auto-remove.
- [ ] **Run LibChecker on full library** - `version`, `explicit`, filename check, album subfolder rule, and inverse genre check are all new since the last clean run. All likely to surface hits. Fix issues, then commit library + report.
- [ ] **First real integration run using the program** (not manual) - use dry-run first, confirm all routing looks right, then real run. This is the goal that was marked ACHIEVED 2026-04-09 at the code level; this tier proves it empirically. **Routing capability as of 2026-04-25:** auto-routes Artist songs (existing folders + scan-ahead for 3+ threshold), Musivation, Motivation. Sources/Films/Shows/Anime NOT auto-routed - those fall to Misc and require manual folder-picker redirection (TIER 3 item). For a batch with only artist songs, program is fully ready.
- [ ] **Deep dive: audit full library against Music-Library-Rules.md** - scan AudioMirror XMLs, cross-reference every track against the rules doc, produce a violations/gaps report. Then confirm LibChecker catches everything the doc mandates. Goal: a clean LibChecker run means full conformance.
  - *Partial progress (2026-04-09)*: rules gap analysis done. Added `CheckAlbumSubfolderRule()` and `CheckGenreVsFolder()`. Remaining: run LibChecker on the full library, scan AudioMirror XMLs for violations not caught by LibChecker.

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

- [ ] **Refactor Constants.cs path builders - the 5x `".."` chains are ugly** - `MirrorRepoPath`, `ReportsPath`, `LibCheckerExceptionsPath`, `LogsPath` all do `Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "..."))` with hardcoded `..` counts matching the `project/AudioManager/bin/Release` depth. Fragile and ugly. Better: a single `RepoRoot` helper that walks up until it finds a sentinel file (`CLAUDE.md`, `README.md`, or `.git/`), then all paths become `Path.Combine(RepoRoot, "reports")` etc. Isolates the depth-counting to one place and self-heals if the build output path ever changes.
- [ ] **Run-state tracking** - RivalsVidMaker has `data/state.json` tracking per-output status with timestamps. AudioManager has no memory of "did I already integrate this NewMusic batch?" - re-running on an empty folder silently does nothing with no audit trail. Add `data/state.json` (or similar) tracking integration runs: timestamp, source folder fingerprint, files moved, outcome. Surfaced in launcher menu ("Last run: Apr 10, 42 files moved, clean").
- [ ] **Simplify launch.bat - move menu logic into Program.cs** - RivalsVidMaker's `run.bat` is 2 lines; all menu/mode logic lives in Python. AudioManager's `launch.bat` is 91 lines with its own menu, duplicating CLI arg logic already in `Program.cs`. Refactor: `launch.bat` becomes a ~5-line wrapper that just builds + runs `AudioManager.exe` with no args; all menu logic lives in Program.cs where it's testable and debuggable.
- [ ] **Add `libchecker-exceptions.example.xml`** - RivalsVidMaker commits a `config.example.json` placeholder; real config gitignored. AudioManager commits the real `libchecker-exceptions.xml` directly. Add an example template so the repo demonstrates the format without baking in personal exceptions. (Minor - current file is already sanitised, but the pattern is cleaner.)
- [ ] **Split `scripts/` into `scripts/` and `scripts/once_off/`** - RivalsVidMaker separates ad-hoc / one-time scripts from production launchers. AudioManager mixes everything. Minor cleanup.
- [ ] **Dry-run covers tag changes and renames** - currently dry-run only covers moves (because tag changes and renames aren't implemented at all). When those features are added, ensure dry-run prints them.
- [ ] **Generate reports in markdown (.md) instead of .txt** - existing reports are plain text. Markdown output with headers, formatting, and better structure improves readability when viewing in an editor or on GitHub. Rename output files to `.md` and use basic markdown syntax: `# Title`, `## Section`, `- bullet points`, code blocks for file paths or error details. Backwards-compatible: dry-run and LibChecker reports should both switch.
- [ ] **Auto-migrate existing Misc songs when scan-ahead promotes an artist** - currently flagged for MANUAL migration (deemed too risky to auto-move existing library files). Revisit with a confirmation gate after tests exist.
- [ ] **Sources/ routing not implemented in GetDestDir()** - `Constants.SourcesDir` exists but `MusicIntegrator.GetDestDir()` has no routing logic for Sources/Films, Sources/Shows, or Sources/Anime. Films/Shows/Anime tracks currently fall to Misc and require manual folder-picker redirection. `Music-Library-Rules.md` documents the expected routing rules (Films subfolder = film name, Shows subfolder = show name, Anime = separate). 
  - **Challenge:** Automating this is difficult - metadata alone rarely indicates source type. Current approach: if `Album` contains "OST" or "Soundtrack", prompt user for subfolder choice rather than defaulting to Misc.
  - **Alternative approach:** Study existing Sources/Films, Sources/Shows, Sources/Anime tracks for metadata patterns (album names, artist names, genre, etc.) that distinguish them. May reveal rough heuristics for auto-detection, or may confirm that folder-picker prompt is the only reliable option.
  - **Recommendation:** TIER 4 exploratory task - audit existing metadata patterns before deciding if automation is feasible. May need to accept manual folder-picker as permanent solution.
- [ ] **README: mention folder picker** - integration pipeline has an interactive folder browser when user rejects Misc routing. Brief mention in README integration section.
- [ ] **README: list LibChecker checks** - README says "full library validation" but doesn't list what LibChecker validates (filename format, unwanted tag strings, missing tags, album cover count, compilation flag, duplicates, artist-folder matching, album subfolder rule, misc folder review, genre-folder consistency, Sources OST check).
- [ ] **Audit libchecker-exceptions.xml - distinguish bug fixes from genuine exceptions** - review all exceptions to identify which are workarounds for LibChecker regex bugs (e.g., "version" in legitimate titles like Alan Watts tracks) vs. genuine tracks that need exemption (e.g., Eric Thomas bonus content). For each regex-bug workaround, ensure there's a corresponding TIER 1 code improvement in IDEAS. Goal: exceptions.xml contains only track-specific needs, not rule improvements. Low priority - documents intent rather than changing behaviour.
- [ ] **AudioMirrorCommitter safety check - prevent commit on ANY issues, not just LibChecker** - currently skips auto-commit only if LibChecker reported hits, but doesn't account for other issues like unexpected file extensions (discovered during Reflector mirror phase). Update check: if Reflector found unexpected extensions OR LibChecker reported hits, skip commit and notify user. Goal: auto-commit only runs when entire pipeline is completely clean.
- [ ] **Re-enable AudioMirrorCommitter auto-commit** - currently disabled (shows manual instructions instead). Re-enable when program is more mature and proven stable on real library operations. Auto-commit was too risky during active development. Prerequisites: (1) TIER 1 all verified, (2) several weeks of stable runs with zero accidental data loss, (3) both safety checks above in place. Low priority - manual commits are safe and auditable.
  - **Note:** Old auto-commit logic is commented out in `AudioMirrorCommitter.TryCommit()` (lines ~60-85) for easy re-enable. Uncomment when prerequisites met.

---

## TIER 4 - FUTURE (Lower Priority / Nice-to-Have)

**Goal: exploratory features and advanced enhancements, tackled after core tiers are stable.**

- **Review mode - library pruning / song-by-song decision tracking** - the library only grows; it needs a structured way to shrink. Add a new `review` mode that walks every song one by one, shows context (tags, folder, optional play count / popularity / lyrics), and asks: keep / remove / defer. Every decision is persisted to a config file (e.g. `config/review-decisions.xml` or similar) with: song fingerprint (artist + title or file hash), decision, date, reason. Old decisions are re-surfaced periodically (e.g. after 12 months) so the review is not one-shot - tastes change, a "keep" today may be a "remove" next year.
  - **Removal candidate signals** (surfaced in review mode to guide decisions, not auto-removed):
    - **Low play count** - cross-reference against iTunes, Last.fm, and/or Spotify listening history. A song never played in 2+ years is a prime candidate.
    - **Negative lyrical/emotional tone** - screen lyrics (via a lyrics API or local cache) and flag songs with heavily negative/depressive/aggressive content on the theory that repeated subconscious exposure shapes mood. This is subjective and must stay advisory, not automated.
  - **Auditable, reversible:** decisions live in a committed config file; a "remove" decision is the review-mode verdict, not an immediate file delete. Actual removal is a separate explicit step after review is complete, with dry-run first.
  - **Integrates with existing infra:** can read from AudioMirror XML (existing scan target), reuse LibChecker exception pattern (external XML config), reuse dry-run pattern from MusicIntegrator.
- **"My Edits" tracking** - detect locally edited songs by comparing duration to official track (>3-4s diff = protected from overwrite).
- **Parody/original song pairing detection** - flag songs where a parody and its original are both in the library.
- **Album completion detection** - cross-reference library against Spotify/MusicBrainz; flag where 50%+ of an album is owned.
- **Fuzzy artist name matching** - handle artist name variations during routing ("The Beatles" vs "Beatles", featured artist formatting differences). Only matters at scale.
- **Fuzzy duplicate title matching** - extend the pre-integration duplicate check to catch near-matches (e.g. "Song (feat. X)" vs "Song", "Song - Remix" vs "Song"). Approach: normalise both sides before comparison by stripping featured artist parentheticals, stripping remix/edit/version suffixes, collapsing whitespace. Blocked by: the exact-match duplicate check (TIER 0) must be in place first.

---

## Settled / not doing

- **AudioMirror as primary scan target** - already implemented and correct. AudioMirror XML is the source of truth for all analysis and LibChecker runs. The actual audio files are never touched during analysis. Safer, faster, version-controlled. Any future analysis tools should read from AudioMirror XML, not audio files directly.
- **Full LibChecker unit test suite** - ROI not worth it (~400+ lines, 6-12 month payback). Add tests incrementally when rules change.
- **Full integration test with fake MP3s** - too heavy, dry-run already covers this.

---

## See Also

- `docs/HISTORY.md` - completed features, settled design decisions, parked ideas
- `docs/Music-Library-Rules.md` - canonical rules for library structure
- `docs/NewMusic-Integration-Plan-20260308.md` - past batch integration (March 2026 batch A)
- `docs/NewMusic-Integration-Plan-20260407.md` - past batch integration (April 2026)
- `docs/AudioMirror-Format.md` - AudioMirror XML format and repo info

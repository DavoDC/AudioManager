# Ideas & Future Work

Single source of truth for all pending work. Settled decisions and completed features -> `HISTORY.md`.

Phases run top-to-bottom. Don't start Phase N+1 until Phase N is done, unless explicitly parked.

---

## Phase 0 - UNBLOCK: make the program runnable via launch.bat

**Top priority. Nothing else matters until `scripts/launch.bat` builds and runs end-to-end.**

- [x] **Fix `Platform=x86` -> `Any CPU` in launch.bat and CLAUDE.md** (done 2026-04-10)
- [x] **Register `AudioMirrorCommitter.cs` in csproj** (done 2026-04-10)
- [ ] **Claude to test-build launch.bat end-to-end himself** - actually run MSBuild + smoke-test each mode (analysis, dry-run, integrate) before handing back. No more "try it again" round trips. See `feedback/feedback_test_build_fixes_yourself.md` in workspace memory.
- [ ] **Add a CRITICAL note to CLAUDE.md about the csproj file-registration gotcha** (done 2026-04-10 - see Build and Run section)

---

## Phase 1 - First real run of the pipeline

**Goal: prove the completed integration pipeline works on real data.** All features are built; this is the validation phase.

- [ ] **Run LibChecker on full library** - `version`, `explicit`, filename check, album subfolder rule, and inverse genre check are all new since the last clean run. All likely to surface hits. Fix issues, then commit library + report.
- [ ] **First real integration run using the program** (not manual) - use dry-run first, confirm all routing looks right, then real run. This is the goal that was marked ACHIEVED 2026-04-09 at the code level; this phase proves it empirically.
- [ ] **Deep dive: audit full library against Music-Library-Rules.md** - scan AudioMirror XMLs, cross-reference every track against the rules doc, produce a violations/gaps report. Then confirm LibChecker catches everything the doc mandates. Goal: a clean LibChecker run means full conformance.
  - *Partial progress (2026-04-09)*: rules gap analysis done. Added `CheckAlbumSubfolderRule()` and `CheckGenreVsFolder()`. Remaining: run LibChecker on the full library, scan AudioMirror XMLs for violations not caught by LibChecker.

---

## Phase 2 - Modernise: .NET 8 migration + minimal tests

**Goal: eliminate the whole class of build-break bug that cost us Phase 0 time, and pin down the highest-risk code paths.**

These two items are grouped because the migration makes adding tests trivial, and the tests need the SDK-style project to avoid the exact csproj-registration bug we just hit.

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

## Phase 3 - Gaps vs RivalsVidMaker / SBS_Download

**Goal: close the structural gaps identified in the 2026-04-10 comparison.** These are polish / robustness, not blockers.

- [ ] **Run-state tracking** - RivalsVidMaker has `data/state.json` tracking per-output status with timestamps. AudioManager has no memory of "did I already integrate this NewMusic batch?" - re-running on an empty folder silently does nothing with no audit trail. Add `data/state.json` (or similar) tracking integration runs: timestamp, source folder fingerprint, files moved, outcome. Surfaced in launcher menu ("Last run: Apr 10, 42 files moved, clean").
- [ ] **Simplify launch.bat - move menu logic into Program.cs** - RivalsVidMaker's `run.bat` is 2 lines; all menu/mode logic lives in Python. AudioManager's `launch.bat` is 91 lines with its own menu, duplicating CLI arg logic already in `Program.cs`. Refactor: `launch.bat` becomes a ~5-line wrapper that just builds + runs `AudioManager.exe` with no args; all menu logic lives in Program.cs where it's testable and debuggable.
- [ ] **Add `libchecker-exceptions.example.xml`** - RivalsVidMaker commits a `config.example.json` placeholder; real config gitignored. AudioManager commits the real `libchecker-exceptions.xml` directly. Add an example template so the repo demonstrates the format without baking in personal exceptions. (Minor - current file is already sanitised, but the pattern is cleaner.)
- [ ] **Split `scripts/` into `scripts/` and `scripts/once_off/`** - RivalsVidMaker separates ad-hoc / one-time scripts from production launchers. AudioManager mixes everything. Minor cleanup.

---

## Phase 4 - Small remaining sub-items from completed features

**Goal: close the minor gaps left inside otherwise-done features.**

- [ ] **Refactor Constants.cs path builders - the 5x `".."` chains are ugly** - `MirrorRepoPath`, `ReportsPath`, `LibCheckerExceptionsPath`, `LogsPath` all do `Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "..."))` with hardcoded `..` counts matching the `project/AudioManager/bin/Release` depth. Fragile and ugly. Better: a single `RepoRoot` helper that walks up until it finds a sentinel file (`CLAUDE.md`, `README.md`, or `.git/`), then all paths become `Path.Combine(RepoRoot, "reports")` etc. Isolates the depth-counting to one place and self-heals if the build output path ever changes.
- [ ] **Dry-run covers tag changes and renames** - currently dry-run only covers moves (because tag changes and renames aren't implemented at all). When those features are added, ensure dry-run prints them.
- [ ] **Auto-migrate existing Misc songs when scan-ahead promotes an artist** - currently flagged for MANUAL migration (deemed too risky to auto-move existing library files). Revisit with a confirmation gate after tests exist.
- [ ] **LibChecker auto-run as second validation layer after integration** - analysis mode already re-runs LibChecker fully; integrate mode doesn't. Add it so a post-integration LibChecker hit immediately flags a broken run.
- [ ] **README: mention folder picker** - integration pipeline has an interactive folder browser when user rejects Misc routing. Brief mention in README integration section.
- [ ] **README: list LibChecker checks** - README says "full library validation" but doesn't list what LibChecker validates (filename format, unwanted tag strings, missing tags, album cover count, compilation flag, duplicates, artist-folder matching, album subfolder rule, misc folder review, genre-folder consistency, Sources OST check).

---

## Phase 5 - Lower priority / future

**Goal: nice-to-haves, parked until the above phases are done.**

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

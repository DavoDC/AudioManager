# Ideas & Future Work

Single source of truth for all pending work. **CLI only.** GUI planning: `GUI-ROADMAP.md`. Completed items -> `HISTORY.md`.

Items are tiered by priority. Do not advance to the next tier until the current tier is verified on real data.

---

## TIER 1 - BLOCKING

**Goal: deliver auto-routing for known cases - eliminate confirmation fatigue. Prerequisites must be verified on real data first.**

**TIER 1 threshold:** anything that would cause a LibChecker warning belongs here, regardless of where or when it was discovered. Routing gaps, rule divergence, config omissions - all TIER 1 if LibChecker would fire on it.

**ACTIVE FOCUS:** Routing tests (GetDestDir) are the top Tier 1 item - start here. Integration bugs below are code-ready; verify at next integration batch. Tests pay back every subsequent fix session.

- [ ] **Automated tests - Routing correctness (GetDestDir)** - Parameterize `GetDestDir()` with `libraryPath` so tests can pass a temp directory. Write 5-6 routing scenarios covering the most common paths. Test infrastructure (Assert.cs, --test runner) is already in place.
  - **Code change:** Add `string libraryPath = null` optional parameter to `GetDestDir()`. When null, falls back to `Constants.AudioFolderPath` - no production caller change needed. Tests pass a temp path.
  - **Test helper:** `Tests/RoutingFixtures.cs` - static `CreateLibraryFixture(string[] artistFolders, string[] musivationFolders)`: creates temp dir with realistic structure, returns tempRoot. Cleanup in `try/finally`.
  - **Test scenarios:** (1) 3+ songs same album -> album subfolder created; (2) 1-2 songs -> Singles/; (3) existing artist folder -> routes into it; (4) Musivation artist -> Musivation/ subfolder; (5) no artist match -> Misc; (6) new artist + 3+ songs -> new Artists/ folder created.
  - **csproj registrations required:** Tests/RoutingTests.cs, Tests/RoutingFixtures.cs.
  - **Payback:** Catches routing regressions without any dry run. Highest-risk module - wrong routing = files moved to wrong library location.

---

## TIER 2 - QUALITY

**Goal: improve UX, add test coverage, and audit metadata quality.**

- [ ] **QUICK WIN: Consistent `--no-pause` flag across all bats** - All bats should support the same two-mode contract: no args = `cmd /k` at end (human - window stays open to read output); `--no-pause` = clean `exit /b` with correct code (Claude - no blocking, no interactive prompts). Currently inconsistent: build.bat has it, verify.bat exits cleanly but has no `cmd /k` for human use, test.bat and launch.bat have unconditional `cmd /k` with no `--no-pause` escape.
  - **Pattern for every exit path:** `if not "%1"=="--no-pause" cmd /k` then `exit /b N`. Remove all standalone `pause` calls (redundant when `cmd /k` keeps window open).
  - **launch.bat:** change `call "%~dp0dev\build.bat"` to `call "%~dp0dev\build.bat" --no-pause` (always - launch.bat controls its own output; intermediate pause from build is wrong UX).
  - **CLAUDE.md update:** change note from "Claude must only use verify.bat or build.bat --no-pause" to "all bats support `--no-pause`; always pass it when calling any bat from Claude".

- [ ] **Test logging: write test results to logs/ for debugging** - Currently `--test` prints to console only. For debugging failures (especially when Claude runs tests as part of a session), output should also go to `logs/test-{timestamp}.log`. Simple change: TestRunner.Run() writes the same [PASS]/[FAIL] output to a log file using the same logs/ folder as analysis/integrate. Payback: when a test fails mid-session, the log file shows exactly what broke and what the assertion values were without needing to re-run manually.

- [ ] **Dry-run as routing regression test (JSON manifest, no MP3 files)** - Dry run already IS an integration test - routing logic runs for real, no file moves. What's missing is (a) controlled input without real NewMusic, and (b) an assertion mechanism. Two approaches, prefer B:

  **Approach A - Minimal MP3 fixtures (simpler to build, messier repo):**
  - Commit tiny .mp3 files to `test-fixtures/NewMusic/` with controlled ID3 tags
  - Add `--test-new-music-path <path>` to override `Constants.NewMusicPath`
  - Problem: TagLib# requires a valid audio frame - pure header-only files fail. Files must contain a real (silent) audio frame, meaning actual binary MP3s in the repo.

  **Approach B - JSON routing manifest (no binary files, recommended):**
  - Add `--routing-manifest <path>` flag that bypasses file scanning and TagFixer entirely
  - Manifest JSON defines virtual tracks (artist, title, album, filename) + expected destination per track
  - MusicIntegrator loads manifest, builds Track objects directly, runs GetDestDir + scan-ahead for real
  - Commit `test-fixtures/routing-manifest.json` - pure text, version-controllable, human-readable
  - Exits code 1 if any actual destination != expected destination, 0 if all match

  ```json
  [
    {"filename": "Known Artist - Song.mp3", "artist": "Known Artist", "title": "Song", "album": "Known Album", "expectedDest": "Artists\\Known Artist\\Singles\\"},
    {"filename": "New Artist - Song1.mp3", "artist": "New Artist", "title": "Song1", "album": "Debut", "expectedDest": "Misc"},
    ...
  ]
  ```

  **What Approach B tests:** GetDestDir routing, scan-ahead (3-song threshold), Misc migration, Musivation routing, duplicate detection via AudioMirror lookup.
  **What it doesn't test:** TagLib# tag reading, TagFixer mutations, file renaming (those are separate concerns - unit tests cover TagFixer pure functions; a separate MP3-based test could cover TagFixer I/O).

  **Pairs with `--json-output` (next item)** - json-output is the output side (parse real dry-run results); the manifest is the input side (inject controlled test tracks). Together they form a full regression harness. The manifest approach is higher value first - input control is the harder problem.

  **Scenarios to cover:** (1) artist with existing library folder -> Singles/; (2) 3+ songs same new artist -> album subfolder; (3) 1-2 songs new artist -> Singles/ then Misc; (4) Musivation artist -> Musivation/ subfolder; (5) no artist match -> Misc; (6) duplicate detected via AudioMirror -> dupe resolution path.

- [ ] **Machine-readable dry-run output mode (`--json-output`)** - Add a flag that writes dry-run results as structured JSON to stdout (or a file) instead of human-readable text. Enables: (a) Claude to parse routing decisions programmatically for automated assertions, (b) future test harness to diff expected vs actual routing without string parsing. Schema: array of routing decisions per file - `{filename, artist, title, album, destination, reason, confidence, tagChanges[], inBatchDuplicate}`. Duplicates and Misc migrations also included. Standard text output stays unchanged for human use; JSON mode is additive. Payback: makes Claude's dry-run verification more precise (flag exact routing regressions, not just visual inspection) and paves the way for automated regression tests once test infrastructure exists.

- [ ] **Automated tests - long-term: broad program coverage** - TagFixer tests (done) and routing tests (Tier 1) deliver the foundation first. This entry covers ongoing expansion once routing tests are stable. Expand only when a real bug escapes current test coverage - never speculatively.
  - **Motivation (unchanged):** Each fix session currently requires 2-3 manual dry run + force regen cycles. Every module covered by a test eliminates that cycle for that module. Real integration (May 2026) found metadata edge cases dry-run missed - tests for the same logic would have caught several earlier.
  - **Infrastructure in place:** Inline `--test` flag, 20-line Assert class, test.bat, launch.bat integration. DIY - no xUnit, no separate project. Old-style csproj manual registration + no VS test runner in the build workflow makes a framework overkill.
  - **Expansion rule:** Add a test when a real bug escapes current coverage. Not before. Signal: if total test count exceeds 50 and no bug has escaped since last expansion, the suite is adequate for current session cadence.
  - **Expansion candidates (in value order, after routing tests are stable):**
    - LibChecker rules: one test per rule (CheckAlbumSubfolderRule, CheckGenreVsFolder, etc.) - verify each rule fires on known-bad input and stays silent on clean input. Especially valuable for the "version/bonus" regex item in TIER 1.
    - TagFixer full pipeline: real .mp3 test fixture -> run TagFixer -> assert ID3 tags changed correctly. Covers the file I/O layer that the existing pure-function tests don't touch.
    - Reflector: snapshot test - known track -> verify XML output format stays stable across refactors.
    - Analyser: known library fixture -> verify report numbers are correct (track counts, genre distribution).
    - AudioMirrorCommitter: verify commit is skipped when LibChecker has hits; verify it runs on force regen + clean run.
  - **Scope discipline:** Test feature behavior, not individual function internals. "Artist casing is preserved end-to-end" not "ExtractAndFixArtists() branch 47". Internals are tested indirectly; changing internals should not break tests if behavior is unchanged.

- [ ] **Thin bats - Phase 2: move interactive menu into Program.cs** - Phase 1 (file moves) done 2026-05-28. Remaining: move all menu logic from launch.bat into Program.cs so launch.bat becomes a pure ~10-line launcher.
  - **launch.bat:** Shrinks from 83 lines to ~10. Calls `dev\build.bat` then `AudioManager.exe` (no args). Exe shows interactive menu. Move timing to here.
  - **Program.cs interactive menu (PromptMode):** Expand from 2 options to 3: (1) Analysis, (2) Analysis (Force Regen), (3) Integrate. Collapse the second Force Regen prompt into the main menu. No "Run Tests" in the interactive menu - dev tool, wrong context.

- [ ] **Feature: Clean up NewMusic folder after real integration** - After real integration completes, automatically clean up the NewMusic source folder (`C:\Users\David\Downloads\NewMusic`). Steps: (1) check for remaining files - if any files exist, warn user and abort cleanup (do NOT delete); (2) if 0 files remain, delete all empty subdirectories and the folder itself. Gate: file count only - if 0 files, the folder is safe to delete regardless of LibChecker state (empty folder cannot cause data loss). No LibChecker gate needed. Rationale: after May 2026 run, 2 empty subdirectories were left behind (`Akon - BEAUTIFUL DAY/` and `Shaboozey - Where I've Been, Isn't Where I'm Going_ The Complete Edition/`). Current state as of 2026-05-26: 0 files, 2 empty subdirectories - safe to delete now.

- [ ] **Compilation album track routing** - DECISION NEEDED - run /think on: "Where should compilation album tracks route when multiple primary artists exist?" User preference framing: tracks where the primary artist has an existing folder -> their `Singles/`; tracks where no artist folder exists -> `Compilations/<album>/` (e.g. `C:\Users\David\Audio\Compilations\`); minimize Misc; avoid single-track folders. Detection signal: compilation = multiple distinct primary artists across tracks in the same album. Applies to cases like 'Barbie The Album'.

- [ ] **Album art dimensions audit and enforcement** - Library has no enforced minimum for embedded album art size. Three-phase approach: (1) **Capture** - extend Reflector to write `<CoverWidth>` and `<CoverHeight>` fields into AudioMirror XMLs (TagLib# exposes `tag.Pictures[0]` with width/height); (2) **Analyse** - after a full regen, scan XMLs to produce a distribution of cover dimensions across the library (histogram: how many tracks at 500x500, 600x600, 300x300, etc.) - let the data reveal where the majority sits before picking a target; (3) **Enforce** - once a sensible minimum is agreed (e.g. 500x500), add a LibChecker rule flagging tracks below that threshold, and optionally warn in TagFixer during integration if incoming tracks have undersized art. Do not guess a limit before running the analysis.

---

## TIER 3 - POLISH

**Goal: close structural gaps and improve developer experience. Non-blocking.**

- [ ] **Album tag inheritance from folder name for incomplete tags** - If album tag is empty on a file from a subfolder, all tracks route to Singles regardless of batch count. Fix: in MusicIntegrator routing, inherit album name from the folder name as a fallback when album tag is missing. Not blocking. (Part (a) - filename guard for empty artist/title - done 2026-05-26.)

- [ ] **Remove RoutingConfidence enum - replace with `bool isNewFolder`** - [code smell] The enum had three values: `Certain`, `Likely`, `Uncertain`. `Uncertain` is now dead code (Misc routes as Certain since 2026-05-25). `Likely` means "scan-ahead is creating a new Artists/ folder" - not a confidence level, a structural note. The enum was designed for a world where some routes prompted the user; that world no longer exists. **Fix:** Replace the enum with `out bool isNewFolder` in `GetDestDir()`. Display: `[AUTO]` vs `[AUTO - new folder]` (more accurate than `[AUTO (likely)]`). **Broader principle this surfaces:** the `RoutingConfidence` abstraction assumed uncertainty gets resolved at integration time. It should be resolved in the dry-run review loop instead. The correct contract is: iterate dry runs until routing looks right, then integrate - integration always auto-routes, no prompts. The code will fully embody this once the enum is removed.

- [ ] **AudioMirrorCommitter: re-enable auto-commit with specific trigger conditions** - Auto-commit AudioMirror (`C:\Users\David\GitHubRepos\AudioMirror`) when LibChecker is clean. Specific trigger conditions: COMMIT on analysis (force regen) OR integration. DO NOT commit on analysis (non-force regen) - incremental mirror may have stale XMLs, only force regen and integration produce a fully reliable state. Commit format must match existing AudioMirror git log conventions (check `git log` in AudioMirror repo for format). Implementation: uncomment logic in `AudioMirrorCommitter.TryCommit()` (lines ~60-85), add trigger-mode parameter so caller can specify which mode triggered the run. Safety gate: if Reflector found unexpected extensions OR LibChecker reported any hits, skip commit and notify user - auto-commit only when entire pipeline is completely clean. Blocked by: TIER 1 bugs fixed first (stale XMLs from Misc Migration and library replacements must be resolved so LibChecker is clean after integration, otherwise auto-commit never fires).

- [ ] **Comprehensive library audit: validate conformance, analyze patterns, unify rules** - Three tightly-coupled goals: (1) AUDIT: Scan AudioMirror XMLs against Music-Library-Rules.md; produce violations/gaps report. Confirm LibChecker catches all mandated rules. Also scan for systematic metadata brittleness surfaced by Stage 3C findings: casing inconsistencies, character illegality (special chars in filenames), and metadata-vs-folder mismatches - find edge cases BEFORE next batch integration; complements character validation by revealing existing library state. Implementation notes: FEEDBACK-Stage3C.md lines 342-345. *Partial progress (2026-04-09): rules gap analysis done, CheckAlbumSubfolderRule() + CheckGenreVsFolder() added. Remaining: run full library audit, identify gaps.* (2) ANALYSE: Extract patterns from decision XMLs + AudioMirror data (artist folder distribution, album patterns, genre consistency, file counts). Build statistical models to identify high-confidence auto-routing cases. Payback: reduces manual confirmations beyond rule-based routing. (3) UNIFY: Refactor routing rules and LibChecker validation into single `RulesEngine` - rules defined once, consumed by both MusicIntegrator and LibChecker. Prevents sync failures (current: TagFixer SKIPPED but LibChecker FLAGGED because rules diverged). All three feed each other: audit finds gaps -> patterns suggest improvements -> unified rules prevent future divergence. Blocked by: auto-routing stable first (so patterns are meaningful).
  - **Think pass first:** Before any implementation, run `/think` on the audit design. Key questions: what violation types are first-class vs derived, what output format serves both human review and future re-runs, and whether audit logic belongs in the main program or stays external. The think pass resolves this before code is written.
  - **Integration consideration:** The audit script may be worth absorbing into AudioManager itself as a `--audit` mode (alongside `--dry-run`, `--integrate`, etc.). Arguments for integration: (a) audit logic needs the same XML-parsing and rules knowledge already in the codebase - duplication risk if kept external; (b) a built-in mode can be run on demand at any time with no separate toolchain; (c) output can share the existing run log format. Arguments against: audit is infrequent and exploratory - a standalone script is lower risk to implement and easier to iterate. Decide during the think pass; if integrated, it becomes a proper `--audit` CLI flag wired into `launch.bat`.
  - **Script approach (token-saving):** Whether standalone or integrated, the audit produces a structured violations report (count, severity, representative examples per violation type) that Claude reviews as output - not by scanning raw XMLs file-by-file. Dramatically reduces token load and makes the audit repeatable without AI involvement.

- [ ] **Dupe routing UX clarity** - When the duplicate resolver shows `[L] Delete library copy`, it is not obvious that the kept file will be routed/placed during the integration step that follows. Add a one-line note after the dupe resolution prompt: "The kept file will be routed in the next step." Eliminates confusion about where the track ends up.

---

## TIER 4 - FUTURE

**Goal: exploratory features and advanced enhancements, tackled after core tiers are stable.**

- **Refactor CountAkiraTheDonPersonSongs/AlbumSongs - artist as parameter** - [code smell] `CountAkiraTheDonPersonSongs(sampledPerson)` and `CountAkiraTheDonPersonAlbumSongs(sampledPerson, album)` hardcode the artist ("Akira The Don") in the function name. These should become `CountPersonSongs(artist, sampledPerson)` and `CountPersonAlbumSongs(artist, sampledPerson, album)` so the same logic can serve future artists with the same People/ folder structure. Only matters if a second artist with a People/-style structure is added.

- **Sources/ routing not implemented in GetDestDir()** - `Constants.SourcesDir` exists but `MusicIntegrator.GetDestDir()` has no routing logic for Sources/Films, Sources/Shows, or Sources/Anime. Films/Shows/Anime tracks currently fall to Misc and require manual folder-picker redirection. `Music-Library-Rules.md` documents the expected routing rules (Films subfolder = film name, Shows subfolder = show name, Anime = separate).
  - **Challenge:** Automating this is difficult - metadata alone rarely indicates source type. Current approach: if `Album` contains "OST" or "Soundtrack", prompt user for subfolder choice rather than defaulting to Misc.
  - **Exploratory:** Study existing Sources/Films, Sources/Shows, Sources/Anime tracks for metadata patterns (album names, artist names, genre, etc.) that distinguish them. May reveal rough heuristics for auto-detection, or may confirm that manual folder-picker prompt is the only reliable solution.

- **Audit libchecker-exceptions.xml - distinguish bug fixes from genuine exceptions** - Review all exceptions to identify which are workarounds for LibChecker regex bugs (e.g., "version" in legitimate titles like Alan Watts tracks) vs. genuine tracks that need exemption (e.g., Eric Thomas bonus content). For each regex-bug workaround, ensure there's a corresponding TIER 1 code improvement in IDEAS. Goal: exceptions.xml contains only track-specific needs, not rule improvements.

- **Evaluate removing .md integration logs in favour of decision XMLs** - Integration logs (`logs/integration-*.md`) and decision XMLs overlap in content. If XMLs contain all routing information, the .md logs may be redundant. Evaluate what .md logs have that XMLs don't (human-readable narrative vs structured data). If XMLs are sufficient for audit and analysis, remove .md logs to simplify the output.

- **Routing decision analysis mode** - Add a mode that reads decision XMLs, cross-references routing decisions against routing rules code and LibChecker rules, and flags inconsistencies. Produces a report: "these N files were routed to X but LibChecker would flag them as Y". Pairs well with the "Centralise rules" refactor. Exploratory - assess value after the first real integration run produces decision XML data to analyse.

- **Leverage library structure to speed up parsing** - Parser currently scans every XML file on every run regardless of what changed. The library has a predictable folder structure (Artists/{artist}/{album}/, Musivation/, Misc/) - could use this to skip unchanged subtrees. Approach: (a) compare folder mtimes against `LastRunInfo.txt` timestamp to identify unchanged artist dirs and skip re-parsing their XMLs; (b) cache parsed tags per folder keyed on folder mtime; (c) only force full re-parse on `Recreated: True` (force regen). Payback scales with library size - already noticeable at current size, significant once library doubles. Requires benchmarking parse time vs filesystem stat calls to confirm the tradeoff is positive.

- **"My Edits" tracking** - detect locally edited songs by comparing duration to official track (>3-4s diff = protected from overwrite).
- **Parody/original song pairing detection** - flag songs where a parody and its original are both in the library.
- **Album completion detection** - cross-reference library against Spotify/MusicBrainz; flag where 50%+ of an album is owned.
- **Fuzzy artist name matching** - handle artist name variations during routing ("The Beatles" vs "Beatles", featured artist formatting differences). Only matters at scale.
- **Fuzzy duplicate title matching** - extend the pre-integration duplicate check to catch near-matches (e.g. "Song (feat. X)" vs "Song", "Song - Remix" vs "Song"). Approach: normalise both sides before comparison by stripping featured artist parentheticals, stripping remix/edit/version suffixes, collapsing whitespace. Blocked by: the exact-match duplicate check must be in place first.
- **Neural network routing (exploratory)** - Train a simple neural network on AudioMirror library commit history and routing decisions to learn implicit routing patterns instead of defining everything statically. Model input: track metadata (artist, album, tags, file structure). Model output: routing destination. Payback: reduces boilerplate routing rules, evolves with library patterns. Very low priority, exploratory phase only - assess whether domain patterns are learnable and whether ML overhead justifies the benefit.

---

## See Also

- `docs/Development/HISTORY.md` - completed features, settled design decisions, parked ideas
- `docs/Development/GUI-ROADMAP.md` - GUI planning: webapp, tabs, Sonarr/Radarr vision, far-future integrations
- `docs/References/Music-Library-Rules.md` - canonical rules for library structure
- `docs/Historical/NewMusic-Integration-Plan-20260308.md` - past batch integration (March 2026 batch A)
- `docs/Historical/NewMusic-Integration-Plan-20260407.md` - past batch integration (April 2026)
- `docs/References/AudioMirror-Format.md` - AudioMirror XML format and repo info

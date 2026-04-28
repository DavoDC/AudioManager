# History

Completed features, settled design decisions, resolved tasks, and decisions explicitly not implemented.

---

## 2026-04-28 Session 3 - UX polish: dry-run parity, Console.Clear removal, timestamped logging

**UX FIX - Dry-run mode now shows confirmation prompts:** Previously, dry-run would auto-accept all routes without prompting user. Now both dry-run and real mode show `[Y] Accept | [N] Choose folder | [Q] Quit` prompts for every file. Dry-run only differs in action: logs what would happen without moving files. Real mode actually moves files after user approval. This ensures user can validate routing decisions before running real integration.

**UX FIX - Console.Clear() removal:** Removed all 4 `Console.Clear()` calls from routing output that were wiping routing decisions from terminal. Replaced with blank lines (Console.WriteLine()) for visual separation. Users can now scroll back and see all routing decisions that were made during dry-run and real integration.

**UX ENHANCEMENT - Timestamped log entries:** Added timestamps to all routing decision outputs using new `PrintTimestamped()` helper method. Shows exact timing of each file processing (HH:mm:ss format). Reduces duplication and improves code maintainability - previously each output line had `$"[{DateTime.Now:HH:mm:ss}]"` repeated; now uses single helper. Timestamps logged to both console and file via TeeWriter.

**Code refactoring - PrintTimestamped() helper:** Created private method to avoid duplication of timestamp boilerplate. All routing output (proposals, confirmations, moves, errors) now calls PrintTimestamped() instead of repeating `$"[{DateTime.Now:HH:mm:ss}] {message}"`. Makes code cleaner and easier to maintain. Build verified - no compilation errors.

**Result:** All TIER 0 UX prerequisites complete. Ready for user to run real integration via launch.bat with full visibility and control.

---

## 2026-04-28 Session 2 - Routing logic fixes: album subfolder, Akira The Don detection, universal confirmation gates

**PRINCIPLE APPLIED - Universal confirmation gates (user control in early stages):** Changed real integration to require user confirmation for ALL routing decisions (not just Misc/ambiguous routes). Every file now prompts: `[Y] Accept | [N] Choose folder | [Q] Quit`. Gives user full control to verify routing, propose alternatives, or stop and review. This matches the principle: "give user more control in early stages of program, later on can automate when stabilised, heavily tested, etc." Dry-run mode unaffected (still just displays proposals).

**CRITICAL BUG FIX - Akira The Don artist name detection:** Fixed ATD not routing to Musivation in dry-run. Root cause: dry-run mode doesn't modify files, so genre=Musivation tag hasn't been set yet when MusicIntegrator reads for routing. Changed detection to check primary artist name directly (Akira The Don detection now happens before genre check), so it works in both dry-run and real mode.

**CRITICAL BUG FIX - Album subfolder logic (holistic counting):** Implemented correct album subfolder routing that counts songs holistically (library + new batch combined). Rule: if 2+ songs from album exist total, route to album folder; <2 songs -> Singles/ folder. Added `CountAlbumSongs()` method that scans both library (filesystem) and NewMusic (tag reading) to determine final album song count.

**CRITICAL BUG FIX - Akira The Don routing to Musivation/People subfolder:** Implemented special routing for Akira The Don tracks: genre=Musivation triggers detection of sampled person from secondary artist. Routes to `Musivation/Akira The Don/People/{person}/` if 3+ songs exist for that person (holistic count); otherwise -> `Musivation/Akira The Don/Singles/`. Added `CountAkiraTheDonPersonSongs()` helper that counts across library People folders, Singles, and NewMusic batch. Sampled person detection via secondary artist (after first semicolon in TPE1).

**ENHANCEMENT - 3+ song rule for Akira The Don People folders:** Implemented scan-ahead-style rule for Akira The Don sampled persons, matching the Artists folder logic: 3+ songs from same person creates dedicated folder, <3 songs go to Singles. This prevents scattered single songs while ensuring organizational consistency.

**VERIFICATION - Filename regeneration:** Confirmed TagFixer already handles filename regeneration correctly. Files are renamed during tag fixing step (`newFilename = {cleanedArtists} - {cleanedTitle}.mp3`), so filenames always match final tag values by the time integration routing begins.

**Documentation updated:**
- Music-Library-Rules.md: Updated Akira The Don section with 3+ person rule and holistic counting explanation
- Both rules now reference the holistic count principle (library + batch combined, not batch-only)

**Code changes:**
- MusicIntegrator.cs: Modified `GetDestDir()` to detect Akira The Don and handle People subfolder routing with 3+ count
- Added two helper methods: `CountAlbumSongs()` and `CountAkiraTheDonPersonSongs()` for holistic counting
- Build verified - no compilation errors

**Ready for testing:** All three routing logic bugs fixed. Now ready for user to run dry-run integration to verify routing decisions match expected patterns before real integration.

---

## 2026-04-28 Session 1 - Pre-integration fix suite: logging, skip error, Unicode, and separate tag fixing (TIER 0/1)

**P0 Bug Fix - Skip error crash:** Fixed `startIndex cannot be larger than length of string` exception in MusicIntegrator when processing skipped files. Added bounds checking to all Substring() calls that compute lengths from path operations.

**P1 Comprehensive Logging:** Extended TeeWriter to support dual console + file output. Wired up timestamped log files for all modes (analysis, integrate, tagfix) in `logs/` directory. All operations now logged to file automatically - users can scan log files for errors, patterns, and auditing after integration runs. Reused existing TeeWriter instead of creating duplicate LogWriter.

**P1 Unicode Fix:** Replaced non-ASCII Unicode rightwards arrows (→) with ASCII ` ->` in TagFixer output for proper console rendering. Fixed display issues showing delta character (␦) in tag comparison output.

**TIER 0 - Separate tag fixing from integration:** Confirmed TagFixer module runs as separate pre-integration step (line 53 of MusicIntegrator.cs). Integration assumes all input files have clean tags and focuses purely on routing decisions. Separation of concerns complete.

**TIER 1 - Auto-log routing decisions to XML:** Confirmed DecisionLog class implemented and wired into MusicIntegrator. Logs all routing decisions (auto-route, manual selection, etc.) to `decisions.xml` with full track metadata, destination path, routing reason, and dryRun flag. Decision logging at lines 268/287/319/368, saved at line 402. Ready for first real integration run with full audit trail.

---

## Settled / Not Doing

These were considered but explicitly deprioritized or deemed not worth implementing:

- **AudioMirror as primary scan target** - already implemented and correct. AudioMirror XML is the source of truth for all analysis and LibChecker runs. The actual audio files are never touched during analysis. Safer, faster, version-controlled. Any future analysis tools should read from AudioMirror XML, not audio files directly.
- **Full LibChecker unit test suite** - ROI not worth it (~400+ lines, 6-12 month payback). Add tests incrementally when rules change.
- **Full integration test with fake MP3s** - too heavy, dry-run already covers this.
- **Run-state tracking** - idea to track "did I already integrate this batch?" with state.json. Deprioritized because routing decisions.xml (TIER 1) provides batch-level summary implicitly (batch timestamp, file count, outcome via entry count).

---

## 2026-04-27 - TIER 0/TIER 3 features completed: TagFixer, LibChecker exceptions, markdown reports, README updates

Completed suite of safety and quality features spanning TIER 0 and TIER 3:

**TIER 0 - TagFixer module (pre-integration tag cleanup)**
- Created **TagFixer** command that runs before integration to clean raw NewMusic files
- Removes parenthetical phrases from Title/Album tags (e.g., `"Song (feat. Artist)"` -> `"Song"`)
- Renames files per convention: `{artists};{artists} - {title}.mp3`
- Moves featured artists to TPE1 tag (semicolon-separated)
- Sets TCMP=1 on all tracks
- Sets genre for Musivation/Motivation tracks
- Supports dry-run mode showing changes without modifying files
- Unblocks the first real integration run: user runs `tagfix` -> `integrate` -> `analyze` in sequence with zero manual tag work

**TIER 3 - Polish & documentation**
- Added `libchecker-exceptions.example.xml` template showing exception format and matching conditions
- Scripts folder structure verified: one-off Python scripts in `scripts/once-off/`; production launchers in `scripts/` root
- Dry-run coverage complete: TagFixer `--dry-run` shows tag changes, Integrator `--dry-run` shows routing decisions
- Markdown reports: ReportWriter and MusicIntegrator now output `.md` files with headers, bold, bullets, inline code
- Updated README: added folder picker mention to Integration Pipeline section and LibChecker Validations section listing all 11 checks

---

## 2026-04-27 - Constants.cs refactor and LibChecker Sources validation (TIER 1/3)

**Constants.cs refactor (TIER 3)** - Replaced all hardcoded `Path.GetFullPath(Path.Combine(BaseDirectory, "..", "..", ...))` chains with a single `FindRepoRoot()` helper that walks up the directory tree looking for sentinel files (CLAUDE.md, README.md, or .git). All paths now use: `Path.Combine(RepoRoot, "reports")` etc. Eliminates fragile depth-counting and self-heals if build output path changes. Commit: `3a3113e0`.

**LibChecker Sources OST validation (TIER 1)** - Implemented smart folder-to-album matching in `CheckSourcesFolder()`. Official soundtracks in Sources/Shows and Sources/Films are only flagged if album contains the source folder name (e.g., Peacemaker folder requires album "Peacemaker OST") but allows featured tracks to keep original album names (e.g., a-ha's "Hunting High and Low"). Eliminates 7 false positive flags and correctly distinguishes soundtrack-specific album tags from featured-track albums.

---

## 2026-04-27 - Pre-integration duplicate check and post-integration validation (TIER 0)

Implemented two safety features blocking TIER 0 to enable the first real integration run:

**Feature 1: Pre-integration duplicate check**
- Before routing each track from NewMusic, searches AudioMirror XML files for an existing track with the same primary artist AND title
- Comparison is case-insensitive and whitespace-trimmed
- If found: displays the matching library entry path and prompts user with [D]elete from NewMusic, [K]eep and continue, [Q]uit
- Dry-run mode shows what would be deleted without actually deleting
- Prevents accidentally importing songs already in the library

**Feature 2: Post-integration LibChecker auto-run**
- After integration completes successfully (not in dry-run), automatically regenerates AudioMirror and runs LibChecker on the updated library
- Catches broken integration runs immediately (zero issues expected if integration was clean)
- Reports "CLEAN" or "ISSUES FOUND" for user visibility
- Both features unblock the first real integration run by providing automated safety gates: duplicate prevention and post-integration validation

---

## 2026-04-27 - LibChecker stability: eliminated duplicate detection and false positive bugs

Two causally-linked LibChecker bugs fixed in one session, both blocking clean TIER 1 integration runs:

**Fix 1: Duplicate detection now considers all featured artists**
- Changed grouping from `{ Title, PrimaryArtist }` to `{ Title, Artists }`
- Previously: "Twista;CeeLo - Hope" and "Twista;Faith Evans - Hope" wrongly flagged as duplicates
- Now: Only flagged if title AND all artists match exactly
- Verified with full library analysis (5489 tracks) - no false positives

**Fix 2: Eliminated 'feat.' and 'ft.' false positives in titles/filenames**
- Changed unwanted-string detection from simple `Contains` to regex for abbreviations
- Previously flagged mid-word matches: "Identity Theft" (ft.), "NEVER LEFT" (ft.), "NO TEARS LEFT" (ft.)
- Now requires whitespace/punctuation before abbreviation: `(?:^|[\s\-\(\)\[\]\/,])`
- Legitimate featured artists like "(feat. Artist)" or " ft. Artist" still correctly flagged
- Verified: full library analysis, 0 false positives for "feat."/"ft."

Both fixes are in the same file (LibChecker.cs) and address the same outcome: enabling reliable TIER 1 integration runs. Committed separately but documented together as a subsystem stabilization.

---

## Previous entry (2026-04-27) - Fixed LibChecker duplicate detection to consider all featured artists

(Merged into the combined entry above.)

LibChecker's duplicate detection was grouping tracks by `{ Title, PrimaryArtist }` only, incorrectly flagging different songs with the same title but different featured artists as duplicates. Example: "Twista;CeeLo - Hope" and "Twista;Faith Evans - Hope" were wrongly grouped together despite having different collaborators.

Fixed by changing the grouping key from `PrimaryArtist` (first artist only) to the full `Artists` field (all collaborators). Now duplicates are only flagged when both title AND all featured artists match exactly. Verified with full library analysis (5489 tracks) - no false positives, and program correctly identifies true duplicates (of which there are currently zero).

This unblocks TIER 1 integration runs, which depend on reliable duplicate detection to prevent importing songs already in the library.

---

## 2026-04-27 - Fixed build script for VS2026 (MSBuild path update)

Visual Studio 2022 Community updated to the 2026 version (VS 18). The old MSBuild path `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe` was no longer valid. Updated `scripts/build.bat` to use the new path: `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`. Build now succeeds with MSBuild 18.5.4. Verified by running build script end-to-end - AudioManager.exe compiles cleanly.

---

## 2026-04-26 - Pre-integration gate: verify AudioMirror fresh and LibChecker clean

Implemented mandatory safety gate before integration runs. When user invokes `integrate` mode:
1. Regenerates AudioMirror XMLs to ensure current library state is captured
2. Checks if XMLs changed (ignores LastRunInfo.txt timestamp-only updates) - if XMLs changed, mirror is stale
3. Runs LibChecker on the fresh data - if any issues found, must be fixed first
4. Only proceeds to integration if BOTH checks pass: fresh mirror AND clean library

Error messages guide user: "AudioMirror is out of sync with the library" or "LibChecker found issues in the library. Fix library issues before adding new songs."

Prevents corruption risk by ensuring new songs are integrated only when library state is known-good. Tested with real library (5491 tracks) - gate correctly rejects integration when LibChecker finds issues, allows proceed when both conditions met.

---

## 2026-04-25 - Phase 0 complete: program verified runnable end-to-end

All Phase 0 blockers resolved. MSBuild compiles clean. `integrate --dry-run` handles empty inbox correctly. `analysis` runs end-to-end: mirror regenerated (5491 tracks), metadata parsed, stats generated, LibChecker ran, report saved, AudioMirror commit correctly blocked due to LibChecker hits.

- `launch.bat` platform flag fixed (`x86` -> `Any CPU`) - done 2026-04-10
- `AudioMirrorCommitter.cs` registered in csproj - done 2026-04-10
- CRITICAL csproj file-registration note added to CLAUDE.md Build and Run section - done 2026-04-10
- Full end-to-end smoke test (MSBuild + all modes) verified by Claude - done 2026-04-25

---

## 2026-04-10 - Constants.MirrorRepoPath made absolute (cwd-independent)

`Constants.MirrorRepoPath` was built from the relative `ProjectPath = "..\\..\\..\\"` literal, which .NET resolves against the **current working directory**, not the exe location. When launched via `scripts/launch.bat` (cwd = `scripts\`), the relative path walked up too far and resolved to `C:\AudioMirror\` - producing `Could not find path 'C:\AudioMirror\LastRunInfo.txt'` in `AgeChecker..ctor` before anything else could run. Fixed by rebuilding `MirrorRepoPath` with `Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "AudioMirror"))` - same pattern already used by `ReportsPath`, `LibCheckerExceptionsPath`, and `LogsPath`. Also tidied `MirrorFolderPath` to use `Path.Combine` (removes a stray double-backslash). Callers that already wrapped `MirrorRepoPath` in `Path.GetFullPath(Path.Combine(...))` continue to work (idempotent). Verified by running `AudioManager.exe analysis` from a `scripts\` cwd - mirror path now resolves to `C:\Users\David\GitHubRepos\AudioMirror\AUDIO_MIRROR` and the full analysis pipeline runs clean.

---

## 2026-04-10 - Build fixes: launch.bat platform + missing csproj Compile entry

Two build failures surfaced when trying to run LibChecker for the first time since the integration pipeline work:

1. **`scripts/launch.bat` line 19** passed `-p:Platform=x86` to MSBuild, but the solution only defines `Debug|Any CPU` and `Release|Any CPU`. Error: `MSB4126: The specified solution configuration "Release|x86" is invalid`. Fixed to `-p:Platform="Any CPU"`. CLAUDE.md line 52 had the same bad example and was also corrected, so future sessions don't keep copying it.

2. **`AudioMirrorCommitter.cs` was never registered in `AudioManager.csproj`.** The file was created in an earlier session but the old-style .NET Framework csproj requires an explicit `<Compile Include="..." />` entry per file - new files are NOT auto-included. Error: `CS0103: The name 'AudioMirrorCommitter' does not exist in the current context`. Added the Compile entry. Added a CRITICAL guard rule to CLAUDE.md "Build and Run" section so this can't silently recur - any new .cs file must be registered in the csproj.

---

## 2026-04-09 - LibChecker: album subfolder rule + inverse genre check

Two new checks added based on Music-Library-Rules.md gap analysis:

- `CheckAlbumSubfolderRule()`: flags Artists/ tracks violating the album subfolder rule.
  - 2+ songs from same album in `Singles/` instead of an album subfolder.
  - Only 1 song from an album in an album-named subfolder instead of `Singles/`.
- `CheckGenreVsFolder(folder, genre)`: flags tracks with Musivation/Motivation genre that are
  outside their expected folder (inverse of the existing `CheckMainFolderForGenre` check).
- `Constants.SinglesDir = "Singles"` added.

---

## 2026-04-09 - LibChecker exceptions moved to config file

`IsExceptionToRules()` hardcoded whitelists extracted to `config/libchecker-exceptions.xml`. LibChecker now loads exceptions at startup via `LoadExceptions()`. Add new exceptions without recompiling - edit the XML and run. Existing exceptions: KRS-One, Original Rappers, Going To Be Alright, Medicine Man, Edition albums, Soundtrack 2 My Life, Agatha All Along, Eric Thomas bonus interview.

---

## 2026-04-09 - AudioMirror auto-commit, scan-ahead routing, LibChecker IsClean

- `AudioMirrorCommitter.TryCommit()`: after every analysis run, if LibChecker clean and AUDIO_MIRROR changed, auto-commits with "MMM d Update" message and pushes. Skips if issues found or nothing changed.
- `LibChecker.IsClean`: new property. `grandTotalHits` accumulates across all check methods via `PrintTotalHits()`. Prints "LibChecker: Clean" when zero hits. Program.cs detects this from captured output.
- `RunScanAhead()`: pre-scans batch MP3 tags + AudioMirror Misc XMLs. Artists hitting 3+ threshold get routed to `Artists/{artist}/` instead of Misc. Preview printed before per-file loop. Existing Misc songs for those artists flagged for manual migration (not auto-moved - risky).
- `GetDestDir()`: scan-ahead HashSet passed in. Routes scan-ahead artists to Artists/ with "[new via scan-ahead]" reason note.

---

## 2026-04-09 - Integration pipeline: pre-process tags, log, routing fix

- `PreProcessTags()` runs on every incoming file before routing: sets TCMP=True on all tracks, sets Genre=Musivation for Akira The Don. Works in dry-run mode (prints what would change without writing).
- `SaveLog()` writes `logs/integration-YYYYMMDD[-dryrun].txt` after every run. Per-file: filename, artists, title, album, destination, tag changes, status. Summary line with totals.
- `GetDestDir()`: fixed routing bug - when artist folder exists but no distinct album, routes to `Singles/` instead of artist root (prevents loose files that LibChecker would flag).
- TagLib resource leak fixed: `TagLib.File.Create()` now wrapped in `using` block.
- `SourcesDir = "Sources"` added to Constants (was the only main folder without one).
- `CheckSourcesFolder()` added to LibChecker: flags Sources/Films and Sources/Shows tracks where Album does not contain "OST".
- Analyser library size: fixed to count only `.mp3` files (previously included .ini, .lnk, etc.).
- `LogsPath` added to Constants (gitignored `logs/` folder).

---

## 2026-04-09 - Batch launcher, CLI args, dry-run mode

- `scripts/launch.bat` - menu-driven launcher, auto-builds via MSBuild, 4 modes (analysis / force-regen / dry-run / real integrate), `cmd /k` to keep window open.
- `Program.cs` - CLI args support: `AudioManager.exe analysis [--force-regen]` or `AudioManager.exe integrate [--dry-run]`. Interactive menu still works when no args passed.
- `MusicIntegrator` - `--dry-run` flag: prints all planned moves without executing any file operations. Shows `[DRY RUN]` prefix. Summary says "Would move" instead of "Moved".

---

## 2026-04-08 - Docs & Knowledge tasks complete

- **Batch A date fixed** - `NewMusic-Integration-Plan-20260407.md` renamed to `20260308`; AudioMirror commit `b8e15b1` confirmed the integration was March 8 2026, not April 7.
- **Music-Library-Rules.md expanded** - Added Sources folder rules (Films, Shows, Anime), full workflow (Stages 1-3), Compilations folder, MP3 conversion, "no album -> use title" rule, Miscellaneous review guidance. Sources folder was completely missing before.
- **Word doc retired as source of truth** - All content from `Audio Folder Organisation Usage Process.docx` extracted into `Music-Library-Rules.md`.
- **Integration plan review** - Both Batch A (20260308) and Batch B (20260407b) reviewed; no additional routing rules found that weren't already in `Music-Library-Rules.md` after the expansion.

---

## 2026-04-08 - Architecture decisions settled

**Integrator stays in AudioManager** - shares Constants, LibChecker, and tag models with the rest of AudioManager. Splitting would require duplicating or packaging shared code with no real gain at this scale. Logical separation (each component has its own entry point) is sufficient.

**Audio reports stay in AudioManager** - AudioMirror is a data repo; generating/storing analysis reports there would blur its purpose. AudioManager owns the tools and the outputs.

**LibChecker output in audio report** - "Checking library..." process lines do not belong in a stats report. Rule: if LibChecker clean, show a single `LibChecker: Clean` line in the report. If issues exist, prominently flag them in the report AND block the AudioMirror commit - fix issues first, then commit both the report and AudioMirror changes.

---

## 2026-04-08 - LibChecker: version/edition suffix detection

Added `"version"` and `"explicit"` to `Constants.UnwantedInfo`. LibChecker now flags titles containing `(Explicit Version)`, `(Album Version)` etc. `(Radio Edit)` and `(Deluxe Edition)` were already caught by the existing `"edit"` entry.

---

## Constants.cs consolidation (already done)

`MiscDir`, `ArtistsDir`, `MusivDir`, `MotivDir` were already defined in `Constants.cs`. Both `LibChecker` and `MusicIntegrator` already read from `Constants` - no duplication existed.

---

## LibChecker owns validation (already done)

`MusicIntegrator`'s only "validation" is a routing precondition (skip files missing artist/title - can't determine destination without them). This is not duplicated LibChecker logic. The boundary was already clean.

---

## ReportWriter - plain static class (already done)

`ReportWriter` is already `internal static class`, not inheriting from Doer. Idea retired.

---

## Analyser stats (already implemented)

Total playback hours, average and median song length, total library size (GB), average file size (MB), track age stats (average, median, oldest, newest), year and decade frequency distribution. All in `Analyser.cs`.

---

## 2026-04-07 - Fix git folder casing

Git tracked folders in old uppercase names (`PROJECT/`, `REPORTS/`, `Docs/`) while they were lowercase on disk. Fixed via two-step `git mv` per folder (required because Windows filesystem is case-insensitive and ignores direct renames). Also updated `REPORTS` path string in `Constants.cs` and comments in `ReportWriter.cs` to match.

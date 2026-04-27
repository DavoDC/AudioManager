# History

Completed features, settled design decisions, and resolved tasks.

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

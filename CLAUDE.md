# AudioManager - Claude Context

## What it does

C# console app for managing a personal music library. Two modes:
- **Analysis** - full pipeline: regenerate AudioMirror XML (Reflector), parse metadata (Parser), generate stats report (Analyser), validate library (LibChecker), save report (ReportWriter), auto-commit AudioMirror if clean (AudioMirrorCommitter)
- **Integrate** - scans `Downloads/NewMusic/`, fixes tags (TCMP, genres, parentheticals, featured artists), routes files into the library, logs routing decisions to XML for audit trail

## Tech Stack

- C# (.NET Framework 4.8)
- TagLib# for ID3 tag reading/writing
- MSBuild for compilation (no Visual Studio needed)

## Project Structure

```
AudioManager/
  CLAUDE.md
  README.md
  .gitignore
  config/                      # libchecker-exceptions.xml (edit without recompiling)
  docs/                        # IDEAS.md, HISTORY.md, design docs
  logs/                        # integration run logs (gitignored, written by MusicIntegrator)
  scripts/                     # launchers and one-off utility scripts
  project/                     # C# solution
    AudioManager.sln
    AudioManager/
      Code/                    # all C# source
        Program.cs             # entry point, mode selection, CLI args handler
        Constants.cs           # all paths and settings (single source of truth)
        TeeWriter.cs           # dual output: screen + log file simultaneously
        Doer/                  # core processing modules (all auto-timed via Doer base class)
          Analyser/            # statistics generation
          AgeChecker.cs        # checks age of mirror, triggers regen if stale
          AudioMirrorCommitter.cs  # auto-commits AudioMirror after clean LibChecker run
          DecisionLog.cs       # logs routing decisions to XML for audit trail
          LibChecker.cs        # library validation rules
          MusicIntegrator.cs   # staging folder scan, calls TagFixer, routes files, logs decisions
          Parser.cs            # parses XML mirror into tag list
          Reflector.cs         # XML mirror creation
          ReportWriter.cs      # timestamped report output
          TagFixer.cs          # comprehensive tag cleanup (TCMP, genres, parentheticals, featured artists, file renames)
        Track/                 # data models: Track, TrackTag, TrackXML
  reports/                     # auto-generated timestamped reports (gitignored, written by C# app)
    YYYY/
      YYYY-MM-DD - AudioReport.txt
```

## Build and Run

### User Workflow (launch.bat)

**Primary:** Always run via interactive menu:
```powershell
.\scripts\launch.bat
```

Menu options:
- 1: Analysis (No Force Regen)
- 2: Analysis (Force Regen)
- 3: Integration (Dry Run) - shows tag fixes + routing decisions preview
- 4: Integration (Real) - applies tag fixes, shows routing decisions preview

launch.bat handles build internally, no separate build step needed.

### Claude: Building the Program

**Always use PowerShell (never Bash) for .bat files. Use full absolute path with --no-pause flag (no cd needed - build.bat uses %~dp0 internally):**
```powershell
& "C:\Users\David\GitHubRepos\AudioManager\scripts\build.bat" --no-pause
```

The `echo "" |` pipes input to skip the "Press any key..." pause. Build completes in ~2-3 seconds without blocking.

**Success looks like:**
```
[BUILD] Compiling AudioManager...
[BUILD] Done. Exe: C:\Users\David\GitHubRepos\AudioManager\scripts\..\project\AudioManager\bin\Release\AudioManager.exe
```

**If build fails:**
- Check `logs\build.log` for full MSBuild output and error details
- Common errors: missing csproj file registration (see CRITICAL below), platform mismatch

### For Scripted/CLI Use

After successful build, exe is at:
```
project\AudioManager\bin\Release\AudioManager.exe
```

Direct CLI:
```
project\AudioManager\bin\Release\AudioManager.exe analysis
project\AudioManager\bin\Release\AudioManager.exe analysis --force-regen
project\AudioManager\bin\Release\AudioManager.exe integrate --dry-run
project\AudioManager\bin\Release\AudioManager.exe integrate
```

**CRITICAL: Legacy csproj format - manual file registration required.**

This is a .NET Framework 4.8 project with the old-style csproj format. New `.cs` files are NOT auto-included in the build.

**Every new file must be manually registered:**
1. Create the `.cs` file in the appropriate folder
2. Open `project\AudioManager\AudioManager.csproj`
3. Add a `<Compile Include="Code\...\NewFile.cs" />` entry in the correct section (e.g., after similar files in Doer/ or Track/)
4. Build to verify: `.\scripts\build.bat`

**If you forget:** Build fails with `CS0103: The name '...' does not exist in the current context`

**Platform constraint:** Solution only defines `Any CPU`. Never pass `-p:Platform=x86` to MSBuild - it will fail with `MSB4126: The specified solution configuration "Release|x86" is invalid`. Always use `-p:Platform="Any CPU"` (which is the default in build.bat).

## Key Paths (from Constants.cs)

- Audio library: `C:\Users\David\Audio\`
- New music staging: `C:\Users\David\Downloads\NewMusic\`
- Reports output: `<repo-root>\reports\` (written by ReportWriter)
- Mirror repo: sits next to this repo at `..\AudioMirror\`

## Data Safety - HIGHEST PRIORITY

**The music library and NewMusic inbox must NEVER experience data loss.**

- The library at `C:\Users\David\Audio\` is the primary copy and is not frequently backed up
- The NewMusic inbox at `C:\Users\David\Downloads\NewMusic\` is also not backed up
- Before ANY file operation (move, rename, delete, overwrite): verify the operation is safe and reversible
- Prefer dry-run mode first - always test against a small sample before running on the real library
- Never delete source files without confirming the destination write succeeded
- When in doubt, do nothing and ask

## AudioMirror as Classification Oracle

**The AudioMirror is the source of truth for facts about the library. Use it to answer classification questions - not name heuristics.**

When you need to determine what something IS (compilation? artist album? genre folder?), read the actual data in AudioMirror rather than pattern-matching names or paths.

**Examples:**
- **Is this album a compilation?** -> Read all XML files in the album folder, collect the full `Artists` field from each track. If many distinct artists appear across tracks = compilation. If the same artist appears on every track = artist album. This works on any album name, including unusual ones like "MEANINGWAVE MASTERPIECES V" that keyword lists would miss.
- **Does an artist have a folder?** -> Check `Directory.Exists(artistFolder)` against the library, not string matching.
- **Is this a Singles folder?** -> Check the path component, not the album tag.

**Why this is better than heuristics:**
- Names drift: compilations aren't always called "Best Of" or "Greatest Hits"
- Heuristics have edge cases: "Volume V" could be an artist's studio album
- Data doesn't lie: if 10 tracks in a folder have 10 different Artists fields, it IS a compilation - no keyword needed

**Applying this principle:** Before writing any keyword list, regex, or name-pattern check, ask: "Does the AudioMirror data already answer this question more reliably?" In most cases, it does. Read the XMLs.

---

## Display Conventions

- **"In AudioMirror"** = the XML entry in AudioMirror repo (`C:\Users\David\GitHubRepos\AudioMirror\AUDIO_MIRROR\...xml`)
- **"In library"** = the MP3 file in the Audio folder (`C:\Users\David\Audio\...mp3`)
- Never say "In library" with an XML path, or "In AudioMirror" with an MP3 path. Match label to file type.
- Duplicate detection surfaces AudioMirror XML paths - display as "In AudioMirror" so the user knows where the detection came from.

## Library Routing Rules (read before touching GetDestDir or any routing code)

These are invariants from Music-Library-Rules.md. Violating them causes files to land in wrong locations.

- **Subfolder before song - at every level.** No song file ever sits directly in an artist folder. Always `Artist/Singles/song.mp3` or `Artist/AlbumName/song.mp3`. This applies at ALL nesting levels - including `Musivation/Akira The Don/People/{Person}/`. `People/Scott Adams/song.mp3` is wrong; `People/Scott Adams/Singles/song.mp3` is correct.
- **Scan-ahead applies everywhere subfolder routing applies.** If you add a new routing path that picks Singles/ vs Album/, apply the same scan-ahead + album-threshold logic used in the Artists/ path. Factor it out - do not duplicate it.
- **Before implementing any new routing path:** read `docs/References/Music-Library-Rules.md` and verify the expected folder structure. Then check whether the subfolder-before-song rule applies.

## Workflow Rules

- **LibChecker-warning priority (TIER 1 threshold):** Any bug, routing gap, or config issue that would cause LibChecker to report a warning is TIER 1 - not TIER 2 or TIER 3. LibChecker warnings mean non-conformant library state that compounds with every integration run. Concrete test: "would `CheckAlbumSubfolderRule()`, `CheckGenreVsFolder()`, or any other LibChecker rule fire on this?" If yes - stop, add to IDEAS.md TIER 1 immediately, address before any other work in the session.

- **AudioMirror commit policy:** never commit AudioMirror or push if LibChecker reported any hits. Fix all issues first, re-run to get a clean run, then commit and push.
- **Check library via filesystem:** check artist/folder existence by browsing `C:\Users\David\Audio\` directly - not by opening the AudioManager app.
- **Tag editing tool: Mp3tag.** When a library file needs its tags fixed manually (e.g. wrong artist casing after integration), advise the user to use Mp3tag. David knows how to use it. Do not suggest VLC or Windows file properties for tag editing.

## Critical Safety Rule

**Only the user (David) executes integration.** Claude implements features and prepares workflows, but stops before running any integration. Integration commands touch real files in AudioMirror repo and NewMusic folder - user must manually trigger via `launch.bat` for data safety and auditability. No exceptions, even for dry-run.

**User workflow:** 
```
.\scripts\launch.bat
→ Select option 3 (Dry Run) or 4 (Real)
```

**Never:** Claude runs `integrate`, `analysis`, or moves files.

## Tag Fixer Constraint

**TagFixer MUST ONLY operate on NewMusic folder.** Never on the Audio library.

TagFixer modifies ID3 tags (TCMP, genres, parentheticals, featured artists) and renames files to match library convention. Tag changes on library files are high-risk: changes propagate to many files, are hard to audit, and difficult to reverse.

**Current implementation:** TagFixer scans `Constants.NewMusicPath` exclusively. Code reads:
```csharp
var files = Directory.GetFiles(Constants.NewMusicPath, "*.mp3", SearchOption.AllDirectories);
```

**Rule:** Never refactor this to accept a `folderPath` parameter or add a library mode. If you need to fix tags in the library, implement it as a separate, read-only analysis tool first (identify which files need fixing), then ask David before touching any files.

## Library Operations Constraint

**In the Audio library, the program can ONLY:**
- Move files from NewMusic into library destinations ✅
- Create destination folders as needed ✅  
- Delete duplicates (user-approved L decisions only) ✅

**The program CANNOT:**
- Modify tags on library files
- Rename library files (except during move)
- Delete files for any reason other than duplicates
- Reorganise existing library structure without user approval

**Current implementation:** Compliant. MusicIntegrator respects these boundaries. All tag modifications happen in NewMusic; library operations are file moves, folder creation, and duplicate deletion only.

**Audit trail:** See `docs/References/SAFETY_CONSTRAINTS.md` for a detailed safety review against these rules.

## Build Scripts - IMPORTANT

**.bat files MUST be run with PowerShell, never Bash.** Windows batch scripts (.bat) don't work in Unix shells. Always use:
```powershell
.\scripts\build.bat
```
NOT:
```bash
bash scripts/build.bat  # WRONG - will fail
```

## Stage 3 Integration Architecture (Developer Reference)

**User workflow:** See Music-Discovery-Workflow.md - Dry-run preview → real run → commit. This section explains how it works for developers.

**The pipeline has two conceptually separate concerns:**

1. **TagFixer (tag cleanup phase):** Reads raw NewMusic files, applies automatic corrections:
   - Removes unwanted words from Title/Album: "(feat. ...)", "(Album Version)", "(Explicit)", etc.
   - Ensures featured artists in TPE1 as semicolon-separated list
   - Renames files to `{artist} - {title}.mp3` format
   - Sets TCMP=1 (prevents iTunes album grouping)
   - Sets genre for Musivation/Motivation tracks per Music-Library-Rules.md
   - **Current state:** User manually cleans tags in MP3Tag before integration (TIER 0 blocker). TagFixer exists but automation blocked on library safety review.

2. **Integration (routing phase):** Routes cleaned files to library destinations:
   - Applies rules from Music-Library-Rules.md (Artists folder, Compilations, Musivation, Motivation, Sources, Miscellaneous)
   - Respects 3-song threshold scan-ahead for album subfolder creation
   - All console output captured to `logs/run-YYYYMMDD-HHmmss.log` with `[HH:mm:ss]` timestamps (via TeeWriter in Program.cs)
   - Dry-run mode: previews all fixes + routing without moving files
   - Post-integration: auto-runs LibChecker to validate library clean

**Design rationale:** Separating tag-fixing from routing makes integration testable and reversible. TagFixer produces clean, ready-to-route files. Integration purely routes. Audit trail documents every decision. If routing fails, it's a routing bug, not a tag issue.

## Current Focus

TIER 1 + TIER 2 complete. TIER 3 in progress. See `docs/Development/IDEAS.md`. Next: RoutingConfidence enum removal or comprehensive library audit. Automated tests deprioritized to bottom of TIER 3.

## Code Invariants (session learnings - update if refactored)

- **Dry-run parity for ALL integration operations** - every step that moves or deletes files must have a dry-run branch that prints what would happen instead. `RunMiscMigration` checks `dryRun` and prints `[DRY RUN] Would move:`. Never add a new integration operation without implementing the dry-run counterpart first.

- **TeeWriter.WriteCharToFile** is the single source of truth for file timestamp logic. `Write(char)` and `WriteLine(string)` both delegate to it. Any TeeWriter change must preserve this. `WriteLine(string)` checks for embedded `\n` and processes char-by-char via `WriteCharToFile` - do not revert to the old single-string write path.
- **RoutingConfidence.Uncertain** is dead code as of 2026-05-25. Do NOT add new uses. TIER 3 item: replace entire enum with `out bool isNewFolder` in `GetDestDir()`.
- **DetermineGenre(artists)** is only called when `ShouldFixGenre` returned true. The else-branch returning `Constants.MotivDir` is intentional - Motivation normalization is the only non-Musivation trigger path.
- **GetRouteCategory(string destDir)** - private static in MusicIntegrator. Strips AudioFolderPath prefix, returns first folder component. Maps "Miscellaneous Songs" -> "Misc". Used by dry-run distribution summary.

## Close-Out Discipline

**Always move completed items to HISTORY.md in the same session they finish.** Never leave a done item as `[ ]` in IDEAS.md. A session spent 45 min investigating an item (routing proposal UX) already implemented in 4dd2e0b9 because it was never closed out. Before ending any implementation session: confirm the item is removed from IDEAS.md and added to HISTORY.md.

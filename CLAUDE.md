# AudioManager - Claude Context

For implementation invariants, architecture detail, and code patterns: read `docs/References/DevContext.md`.

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

Menu options (arrow-key navigable, shown in the exe after build):
- Analysis - standard analysis run
- Analysis (Force Regen) - forces full AudioMirror regeneration
- Integrate - runs dry run first, then prompts "Proceed with real integration? [y/N]"

launch.bat handles build internally; the exe shows the interactive menu (no menu logic in the bat).

### Claude: Building the Program

**Always use PowerShell (never Bash) for .bat files. Use full absolute path with --no-pause flag (no cd needed - build.bat uses %~dp0 internally):**
```powershell
& "C:\Users\David\GitHubRepos\AudioManager\scripts\dev\build.bat" --no-pause
```

Build completes in ~2-3 seconds without blocking.

**Success looks like:**
```
[BUILD] Compiling AudioManager...
[BUILD] Done. Exe: C:\Users\David\GitHubRepos\AudioManager\scripts\dev\..\..\project\AudioManager\bin\Release\AudioManager.exe
```

### Claude: Running Tests (MANDATORY after any C# code change)

**After every C# code change, run tests before committing.** Tests are fast (< 1 second) and catch tag logic regressions immediately. Do not skip this step even for "obviously safe" changes.

```powershell
& "C:\Users\David\GitHubRepos\AudioManager\scripts\dev\verify.bat" --no-pause
```

Runs build + unit tests + routing manifest tests. All must pass before any C# commit.

**NOTE: All bats support `--no-pause` (clean exit for Claude) vs no args (window stays open for human use). Always pass `--no-pause` when calling any bat from Claude.**

If a test fails after a change: fix the code, not the test (unless the test is wrong - state why explicitly).

Files where a change triggers mandatory test run: anything in `Code/` - especially TagFixer.cs, Track/Track.cs, Constants.cs (string constants), and all files in Code/Tests/.

**If build fails:**
- Check `logs\build.log` for full MSBuild output and error details
- Common errors: missing csproj file registration (see CRITICAL below), platform mismatch

**CRITICAL: Legacy csproj format - manual file registration required.**

This is a .NET Framework 4.8 project with the old-style csproj format. New `.cs` files are NOT auto-included in the build.

**Every new file must be manually registered:**
1. Create the `.cs` file in the appropriate folder
2. Open `project\AudioManager\AudioManager.csproj`
3. Add a `<Compile Include="Code\...\NewFile.cs" />` entry in the correct section
4. Build to verify: `.\scripts\dev\build.bat`

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

- **LibChecker-warning priority (TIER 1 threshold):** Any bug, routing gap, or config issue that would cause LibChecker to report a warning is TIER 1. LibChecker warnings mean non-conformant library state that compounds with every integration run. Concrete test: "would `CheckAlbumSubfolderRule()`, `CheckGenreVsFolder()`, or any other LibChecker rule fire on this?" If yes - stop, add to IDEAS.md TIER 1 immediately, address before any other work in the session.

- **AudioMirror dirty state after force-regen = expected, not corruption.** XDocument.Save() writes CRLF; .gitattributes enforces eol=lf storage - so every regen leaves all XMLs "modified" until committed. If AudioMirrorCommitter hangs (known bug), manually run `git -C "<AudioMirror path>" add -A && git commit`. See IDEAS TIER 3 for the one-line write-site fix (XmlWriterSettings NewLineChars).
- **Temporarily disabled checks need tests commented out too.** When a LibChecker check is commented out, its unit tests must also be commented (not deleted) - both re-enable together. Leaving tests active causes false failures.
- **AudioMirror commit policy:** never commit AudioMirror or push if LibChecker reported any hits. Fix all issues first, re-run to get a clean run, then commit and push.
- **AudioMirror rebuild reliability:** Analysis (non-force regen) runs with `Recreated: False` (incremental mirror update) - NOT reliable for LibChecker pass claims or auto-commit. Only analysis (force regen) and integration produce a fully reliable mirror state. Auto-commit must only trigger on force regen or integration.
- **Force regen = canonical fresh-data operation.** Incremental Reflector creates XMLs for new MP3s and refreshes XMLs when the underlying MP3 is newer than its XML (tag edits in Mp3tag). It does NOT remove XMLs for deleted MP3s - force regen handles deletion cleanup (deletes mirror entirely, rebuilds). ParseCache (logs/parse-cache.txt) serves data consistent with XMLs. Diagnosing ghost tracks (deleted MP3 appears in report): run `analysis --force-regen`. See DevContext.md for the three-layer cache architecture.
- **Check library via filesystem:** check artist/folder existence by browsing `C:\Users\David\Audio\` directly - not by opening the AudioManager app.
- **Tag editing tool: Mp3tag.** When a library file needs its tags fixed manually, advise the user to use Mp3tag. Do not suggest VLC or Windows file properties for tag editing.

## Audit Metadata in Version Control

**AudioMirror audit metadata (LastRunInfo.txt) is as much part of the artifact as the data itself.** It lives in git because:
- Answers "when was this mirror regenerated?" for every commit
- Enables audit trail and diagnostics (regen failed? cache stale? force-regen active?)
- Changes on every force-regen, so commits on every regen are expected and acceptable
- Not noise - metadata IS the artifact state

Treat it like you treat the XML files: version-control it, don't relegate it to ephemeral-only.

## Avoid Output-Capture for Control Flow

**Never use string-search of stdout to gate decisions.** Example: detecting "LibChecker: Clean" by grepping Program.cs's captured output. This couples output format to control logic - if LibChecker's message changes, the detection breaks. Better: return explicit status codes or enums from modules, not relying on stdout parsing for branching logic.

## Critical Safety Rule

**Only the user (David) runs real integration.** Real integration moves files from NewMusic into the library - user must manually trigger via `launch.bat` for data safety and auditability.

**`audioTags` list order is non-deterministic** (Parser uses `Parallel.ForEach` + `ConcurrentBag` since 2026-06-27). LibChecker, Analyser, and ParseCache all consume it without order dependency - safe. If any new consumer needs ordered output, sort explicitly after `parser.audioTags` is returned.

**Claude CAN run (read-only, no file moves):**
- `analysis` (and `analysis --force-regen`) - reads library, generates report, no writes except AudioMirror XML regen
- `integrate --dry-run` - previews routing decisions without touching any files

**Always add `--no-auto-commit` when Claude runs force-regen.** AudioMirrorCommitter fires automatically and commits before the diff can be reviewed. Use:
```
& "...\AudioManager.exe" analysis --force-regen --no-input --no-auto-commit
```
After the run: check `git -C AudioMirror diff HEAD`, confirm the diff looks correct, then commit AudioMirror manually.

**Claude CANNOT run:**
- `integrate` (real) - moves files, irreversible

**User workflow for real integration:**
```
.\scripts\launch.bat
-> Select option 3 (Integration) - runs dry run first, prompts "Proceed with real integration? [y/N]"
```

**Claude dev workflow for verifying fixes:**
```
& "C:\Users\David\GitHubRepos\AudioManager\scripts\dev\build.bat" --no-pause
& "C:\Users\David\GitHubRepos\AudioManager\project\AudioManager\bin\Release\AudioManager.exe" integrate --dry-run --no-input
```
`--no-input` skips all interactive prompts. Run after any routing or tag fix to see real output without blocking.

## Tag Fixer Constraint

**TagFixer MUST ONLY operate on NewMusic folder.** Never on the Audio library.

TagFixer modifies ID3 tags and renames files to match library convention. Tag changes on library files are high-risk: changes propagate to many files, are hard to audit, and difficult to reverse.

**Rule:** Never refactor this to accept a `folderPath` parameter or add a library mode. If you need to fix tags in the library, implement it as a separate, read-only analysis tool first, then ask David before touching any files.

## Library Operations Constraint

**In the Audio library, the program can ONLY:**
- Move files from NewMusic into library destinations
- Create destination folders as needed
- Delete duplicates (user-approved L decisions only)

**The program CANNOT:**
- Modify tags on library files
- Rename library files (except during move)
- Delete files for any reason other than duplicates
- Reorganise existing library structure without user approval

**Audit trail:** See `docs/References/SAFETY_CONSTRAINTS.md` for a detailed safety review against these rules.

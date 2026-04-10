# AudioManager - Claude Context

## What it does

C# console app for managing a personal music library. Two modes:
- **Analysis** - full pipeline: regenerate AudioMirror XML (Reflector), parse metadata (Parser), generate stats report (Analyser), validate library (LibChecker), save report (ReportWriter), auto-commit AudioMirror if clean (AudioMirrorCommitter)
- **Integrate** - scans `Downloads/NewMusic/`, pre-processes tags, routes files into the library, saves integration log

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
          LibChecker.cs        # library validation rules
          MusicIntegrator.cs   # staging folder scan, tag pre-processing, file routing
          Parser.cs            # parses XML mirror into tag list
          Reflector.cs         # XML mirror creation
          ReportWriter.cs      # timestamped report output
        Track/                 # data models: Track, TrackTag, TrackXML
  reports/                     # auto-generated timestamped reports (gitignored, written by C# app)
    YYYY/
      YYYY-MM-DD - AudioReport.txt
```

## Build and Run

Build with MSBuild (no need to open Visual Studio):
```
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" project\AudioManager.sln -p:Configuration=Release -p:Platform="Any CPU" -verbosity:minimal
```

Run the compiled exe directly, or use the launcher in `scripts/`.

**CRITICAL: old-style csproj requires manual file registration.** This is a .NET Framework 4.8 project with the legacy csproj format. New `.cs` files are NOT auto-included - you MUST add a `<Compile Include="Code\...\NewFile.cs" />` entry to `project\AudioManager\AudioManager.csproj` whenever you create a new source file, or the build will fail with `CS0103: The name '...' does not exist in the current context`. Always verify the csproj was updated after adding a file.

**Solution only defines `Any CPU` platform.** Never pass `-p:Platform=x86` to MSBuild - it will fail with `MSB4126: The specified solution configuration "Release|x86" is invalid`. Use `-p:Platform="Any CPU"`.

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

## Workflow Rules

- **AudioMirror commit policy:** never commit AudioMirror or push if LibChecker reported any hits. Fix all issues first, re-run to get a clean run, then commit and push.
- **Check library via filesystem:** check artist/folder existence by browsing `C:\Users\David\Audio\` directly - not by opening the AudioManager app.

## Current Focus

See `docs/IDEAS.md` for the full priority list. Integration pipeline complete. Next: first real integration run using the program, then a clean LibChecker run to validate full library state.

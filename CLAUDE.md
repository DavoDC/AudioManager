# AudioManager - Claude Context

## What it does

C# console app for managing a personal music library. Four modes:
- **Mirror** - generates an XML snapshot of the library (for AudioMirror repo)
- **Analyse** - generates timestamped statistical reports saved to `reports/`
- **Audit** - validates library structure, file format, and metadata completeness
- **Integrate** - scans `Downloads/NewMusic/`, validates ID3 tags, moves files into the organised library

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
  docs/                        # IDEAS.md, HISTORY.md, design docs
  scripts/                     # launchers and one-off utility scripts
  project/                     # C# solution
    AudioManager.sln
    AudioManager/
      Code/                    # all C# source
        Program.cs             # entry point and mode selection
        Constants.cs           # all paths and settings (single source of truth)
        TeeWriter.cs           # dual output: screen + log file simultaneously
        Doer/                  # core processing modules (all auto-timed via Doer base class)
          Analyser/            # statistics generation
          Reflector.cs         # XML mirror creation
          MusicIntegrator.cs   # staging folder scan and file routing
          LibChecker.cs        # library validation rules
          ReportWriter.cs      # timestamped report output
        Track/                 # data models: Track, TrackTag, TrackXML
  reports/                     # auto-generated timestamped reports (gitignored, written by C# app)
    YYYY/
      YYYY-MM-DD - AudioReport.txt
```

## Build and Run

Build with MSBuild (no need to open Visual Studio):
```
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" project\AudioManager.sln -p:Configuration=Release -p:Platform=x86 -verbosity:minimal
```

Run the compiled exe directly, or use the launcher in `scripts/`.

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

See `docs/IDEAS.md` for the full priority list. Current goal: finish the new music integration pipeline (Stages 2 and 3) before running on the real library.

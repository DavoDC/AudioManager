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

**Always use PowerShell (never Bash) for .bat files:**
```powershell
cd "C:\Users\David\GitHubRepos\AudioManager"
.\scripts\build.bat
```

**Success looks like:**
```
[BUILD] Compiling AudioManager...
[BUILD] Done. Exe: C:\Users\David\GitHubRepos\AudioManager\scripts\..\project\AudioManager\bin\Release\AudioManager.exe
```

**If build fails:**
- Check `logs\build.log` for full MSBuild output and error details
- Common errors: missing csproj file registration (see CRITICAL below), platform mismatch

**To skip interactive pause (for automation):**
```powershell
echo "" | .\scripts\build.bat
```

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

## Workflow Rules

- **AudioMirror commit policy:** never commit AudioMirror or push if LibChecker reported any hits. Fix all issues first, re-run to get a clean run, then commit and push.
- **Check library via filesystem:** check artist/folder existence by browsing `C:\Users\David\Audio\` directly - not by opening the AudioManager app.

## Critical Safety Rule

**Only the user (David) executes integration.** Claude implements features and prepares workflows, but stops before running any integration. Integration commands touch real files in AudioMirror repo and NewMusic folder - user must manually trigger via `launch.bat` for data safety and auditability. No exceptions, even for dry-run.

**User workflow:** 
```
.\scripts\launch.bat
→ Select option 3 (Dry Run) or 4 (Real)
```

**Never:** Claude runs `integrate`, `analysis`, or moves files.

## Build Scripts - IMPORTANT

**.bat files MUST be run with PowerShell, never Bash.** Windows batch scripts (.bat) don't work in Unix shells. Always use:
```powershell
.\scripts\build.bat
```
NOT:
```bash
bash scripts/build.bat  # WRONG - will fail
```

## Current Focus

See `docs/Development/IDEAS.md` for the full priority list. TIER 0 (safety prerequisites) + TIER 1 (decision logging) complete. Tag fixing and routing decision logging integrated. Next: first real integration run via launch.bat to validate the complete pipeline, then extract routing patterns from decision.xml.

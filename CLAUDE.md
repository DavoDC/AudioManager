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

See "Project Structure" in `docs/References/DevContext.md` for the full tree.

## Build and Run

### User Workflow (launch.bat)

**Primary:** Always run via interactive menu:
```powershell
.\scripts\launch.bat
```

Menu (arrow-key navigable, shown in the exe after build): Analysis, Analysis (Force Regen), Integrate (dry run first, then prompts "Proceed with real integration? [y/N]"). launch.bat handles build internally; the exe shows the menu.

### Claude: Building the Program

**Always use PowerShell (never Bash) for .bat files, full absolute path, `--no-pause` flag** (no cd needed - build.bat uses `%~dp0` internally):
```powershell
& "C:\Users\David\GitHubRepos\AudioManager\scripts\dev\build.bat" --no-pause
```
Completes in ~2-3 seconds without blocking. Success output example: "Build success/failure" in `docs/References/DevContext.md`.

### Claude: Running Tests (MANDATORY after any C# code change)

**After every C# code change, run tests before committing** - fast (< 1 second), catches tag logic regressions. Files that trigger this: anything in `Code/`, especially TagFixer.cs, Track/Track.cs, Constants.cs, Code/Tests/.

```powershell
& "C:\Users\David\GitHubRepos\AudioManager\scripts\dev\verify.bat" --no-pause
```

Runs build + unit tests + routing manifest tests. All must pass before any C# commit. All bats support `--no-pause` (always pass it from Claude). If a test fails: fix the code, not the test (unless the test is wrong - state why).

Legacy csproj format, csproj file registration, the `Any CPU`-only platform constraint, and which MSBuild to invoke directly: see "Build troubleshooting" in `docs/References/DevContext.md`.

### Claude: GUI dev hot-reload - use the trigger, don't kill the process

If `scripts\launch-gui-dev.bat` is running while editing `gui/`, touch/create
`gui/.cache/reload.trigger` once ready instead of `taskkill`/closing the window.
Detail: `docs/References/GUI-Architecture.md` "Dev mode: hot-reload".

### Development priority: Library Intake over Library Insight (confirmed 2026-09-05)

**Library Intake (Acquire, Integration, Tag Fix - the GUI's write/mutate tabs) is the current development focus; Library Insight (Statistics, Library, Mirror, Services - read-only views) is explicitly lower priority for now.** When picking up open work with no other signal, prefer an open Intake item over an open Insight item. See `docs/Development/IDEAS.md` TIER 2 priority-ordering note for the audited item order.

### Claude: GUI visual design - read docs/DESIGN.md first

Before adding or changing any control, panel, table, or layout in `gui/`,
read `docs/DESIGN.md` - palette, alignment/spacing rules, and when a view
should be a table vs a card grid. Every control must reuse the CSS vars and
patterns it defines rather than inventing new styling per tab.

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

## Library Routing Rules

**Before touching GetDestDir or any routing code**, read `docs/References/Music-Library-Rules.md` and see "Library Routing Rules" in `docs/References/DevContext.md` - subfolder-before-song and scan-ahead invariants. Violating them causes files to land in wrong locations.

## Workflow Note

**David's actual integration cadence: one big batch every 2-4 weeks, not daily.** Any UI/report design (GUI dashboards, stats, "recent activity" views) that assumes daily/steady-drip additions will look wrong or empty most days and spike hard on integration day - design for batch-shaped data (group by integration-run/week, not by day). Noted 2026-07-02 during GUI mockup review.

For LibChecker-warning triage, post-integration validation mechanics, AudioMirror commit/regen policy, and audit-metadata rationale, see `docs/References/DevContext.md`.

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

**Claude dev workflow for verifying fixes:** build, then run `integrate --dry-run --no-input` (skips interactive prompts) to see real output without blocking - see "Claude dev verify workflow" in `docs/References/DevContext.md` for the exact commands.

## Tag Fixer Constraint

**TagFixer MUST ONLY operate on NewMusic folder.** Never on the Audio library.

TagFixer modifies ID3 tags and renames files to match library convention. Tag changes on library files are high-risk: changes propagate to many files, are hard to audit, and difficult to reverse.

**Rule:** Never refactor this to accept a `folderPath` parameter or add a library mode. If you need to fix tags in the library, implement it as a separate, read-only analysis tool first, then ask David before touching any files.

## Library Operations Constraint

**What the program can/cannot do to files already in the Audio library:** see "Library Operations Constraint" in `docs/References/DevContext.md`.

## What the Test Suite Can and Cannot Prove

`scripts\dev\verify.bat --no-pause` is the single pass/fail judge - it runs the C# build and unit tests, the routing manifest check, and the GUI pytest suite. A green run proves the logic is right. It can never prove the batch is safe: nothing in the suite observes the exe actually moving files. On any change touching the integration path, treat green as "the logic is right", never as "the run is safe to fire".

The code-change escalation boundary for the GUI is recorded in `docs/Development/IDEAS.md` under the readiness verdict. Growing that guarded set always needs David; shrinking it never does.

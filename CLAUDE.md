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

**Primary:** run `.\scripts\launch.bat` (builds internally; menu: Analysis, Analysis (Force Regen), Integrate with a dry run first, then "Proceed with real integration? [y/N]").

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

While `scripts\Gui.bat` runs, touch `gui/.cache/reload.trigger` instead of killing the window (reload defers while a job runs). Detail: `docs/References/GUI-Architecture.md` "Dev mode: hot-reload".

### Claude: driving and checking the GUI

Run block: `scripts\Gui.bat` (launch), `python -m gui.main --check|--preflight|--stop`, `python -m gui.verify` (one PASS or FAIL line), `python -m gui.capture`, `python -m gui.main --fixture NAME`. State endpoints, `window.__debug`, `ui_check`, CLI parity: `docs/References/GUI-State-Contract.md`. Fonts are bundled; no external URL may enter the GUI. The GUI is desktop-only: no phone-width support or checks (David, 2026-10-23).

**Verification ladder, cheapest first:** (1) unit test `python -m pytest gui/tests -q`; (2) `python -m gui.verify --fast` (pytest + ui_check, about 20s); (3) `python -m gui.cli state` for the DOM-state read (`/state.json`); (4) `python -m gui.capture` for screenshots; (5) Chrome browser tool only for real interaction.

### GUI test suite speed

Measured 2026-10-12: `python -m pytest gui -q` runs 482 tests in about 14 s (465 in about 7.5 s before the fixture-server tests were added); no xdist or sleep removal needed, nothing real sleeps for more than 2.4 s (test_preflight). The ui_check step of `python -m gui.verify` fell from 34 s to 2 s because fixture servers are now addressed as 127.0.0.1 (the name localhost cost 2 s per request on Windows); the Chrome capture step still takes about 98 s and is the bulk of verify. Re-measure with `--durations=15` if the suite passes 2 minutes.

### Development priority

Library Intake (Acquire, Integration, Tag Fix) outranks Library Insight for now; no-signal work picks Intake. Detail: "Development Priority" in `docs/References/DevContext.md`.

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

Label "In AudioMirror" for XML paths and "In library" for MP3 paths, never mixed: "Display Conventions" in `docs/References/DevContext.md`.

## Library Routing Rules

**Before touching GetDestDir or any routing code**, read `docs/References/Music-Library-Rules.md` and see "Library Routing Rules" in `docs/References/DevContext.md` - subfolder-before-song and scan-ahead invariants. Violating them causes files to land in wrong locations.

## Workflow Note

Integration is one big batch every 2-4 weeks: design UI and reports for batch-shaped data, not daily drips ("Integration Cadence" in `docs/References/DevContext.md`).

For LibChecker-warning triage, post-integration validation mechanics, AudioMirror commit/regen policy, and audit-metadata rationale, see `docs/References/DevContext.md`.

## Critical Safety Rule

**Only the user (David) runs real integration.** Real integration moves files from NewMusic into the library - user must manually trigger via `launch.bat` for data safety and auditability.

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

**User workflow for real integration:** `.\scripts\launch.bat`, option 3 (Integration); it dry-runs first and prompts "Proceed with real integration? [y/N]".

**Claude dev workflow for verifying fixes:** build, then run `integrate --dry-run --no-input` (skips interactive prompts) to see real output without blocking - see "Claude dev verify workflow" in `docs/References/DevContext.md` for the exact commands.

## Tag Fixer Constraint

**TagFixer MUST ONLY operate on NewMusic folder.** Never on the Audio library.

TagFixer modifies ID3 tags and renames files to match library convention. Tag changes on library files are high-risk: changes propagate to many files, are hard to audit, and difficult to reverse.

**Rule:** Never refactor this to accept a `folderPath` parameter or add a library mode. If you need to fix tags in the library, implement it as a separate, read-only analysis tool first, then ask David before touching any files.

**Library Operations Constraint:** what the program can/cannot do to files already in the Audio library - see "Library Operations Constraint" in `docs/References/DevContext.md`.

## GUI Code-Change Escalation Boundary

**C#/Python split (decided 2026-09-06, do not relitigate):** keep the language split; never let Python write to the library, reimplement C# logic, or parse AudioMirror XML (2 read-only tag/art exceptions excepted). Reasoning: `docs/References/Architecture-Language-Decision.md`.

**The guarded path:** the path from a decline click to the exe's argument list - `IntegrationState.accepted`/`declined`, the manifest-writing block in `run_execute`, and the construction of `args` - is the only thing standing between a declined file and it being moved for real. A change there is written unattended but reviewed before it ships.

**Everything else in `gui/` is permissionless:** tests, display logic, status labels, styling, docs. Shrinking the guarded set never needs approval; growing it always does.

## What the Test Suite Can and Cannot Prove

See "What the test suite can and cannot prove" in `docs/References/DevContext.md` - a green `verify.bat` proves the logic, never that a real integration batch is safe to fire.

## Webapp standard

This GUI follows a shared webapp standard (checklist of WS ids); backlog items cite WS ids.

# Ideas & Future Work

Single source of truth for all pending work. Settled decisions and completed features -> `HISTORY.md`.

---

## Immediate Actions

- [ ] **David: review all docs before further implementation** - `Music-Library-Rules.md`, `AudioMirror-Format.md`, and the integration plan case studies are the requirements docs for this software. Review them for accuracy and completeness before any more integrator/launcher code is written. Corrections go back into the docs first, then code follows.
- [ ] **Run LibChecker on full library** - "version" and "explicit" added to UnwantedInfo (commit 24340979), filename check added (commit a2f01004). Both likely surface new hits. Fix any found issues, then commit library + report.
- [ ] **Deep dive: audit full library against Music-Library-Rules.md** - Claude to scan AudioMirror XML files, cross-reference every track against the rules doc, and produce a report of violations and gaps. Then review LibChecker source - identify any rules from the doc that LibChecker does not currently enforce, and add the missing checks. Goal: LibChecker should be comprehensive enough that a clean run means the library fully conforms to the rules.

---

## Current Goal

Get to the point where a new music batch can be integrated using the program rather than manually - with dry run, metadata editing, routing, validation, and a confidence report.

---

## Pending - Priority Order

*(Quick wins first, then by dependency order.)*

---

### Docs & Knowledge

*(All done - see HISTORY.md)*

---

### Quick Wins

**LibChecker: move hardcoded exceptions to config file**
`IsExceptionToRules()` in `LibChecker.cs` has hardcoded artist/title/album whitelists (KRS-One, "Original Rappers", "Going To Be Alright", "Medicine Man", "Agatha All Along", Eric Thomas, etc.). Move these to `config/libchecker-exceptions.json` at the repo root so exceptions can be added without recompiling.

---

### Launcher

**Single batch launcher** *(quick win, high priority)*
One `.bat` file, menu-driven. Auto-builds via MSBuild before running.

Top-level menu:
1. Analysis (No Force Regen)
2. Analysis (Force Regen)
3. Integration (Dry Run)
4. Integration (Real)

Rules for all modes:
- Auto-compile via MSBuild before running
- Show output in terminal, log to file, `cmd /k`
- Analysis always runs and saves report to file; integration terminal output must NOT bleed into the report
- If NewMusic folder is empty or doesn't exist in integration modes, skip silently

**Dry run mode** *(data-safety gate)*
MusicIntegrator prints every planned action (tag change, rename, move) without executing any of them. Invoked via launcher option 3 or `--dry-run` flag. Must run before every real integration. Output format must be identical to real run report (minus "moved" status) so the user can diff them.

---

### Integration Pipeline

*(In dependency order - implement top to bottom.)*

**Metadata editing before move**
Before accepting a file move, apply:
- Set genre to `Musivation` for Akira The Don tracks
- Set `IsCompilation = true` (TCMP tag) via ID3v2 on every file before moving
- Filter out non-`.mp3` files

**JSON integration log** *(depends on: Metadata editing)*
Append to `MusicIntegrationLog.json` per run:
- Full metadata fields + decision fields: source, destination, tag changes made, timestamp
- On re-run, check log first before re-deciding (decision history principle)

**Scan-ahead: 3-song threshold and Misc migration** *(depends on: JSON log)*
Before processing a batch, scan all files to calculate post-integration artist counts:
- Artists in batch that will hit the 3+ song threshold -> create artist folder
- Existing Misc songs by those artists -> migrate to new folder at the same time
- Show a preview of all planned moves before executing anything
Without this, files get routed to Misc incorrectly for artists that should get their own folder.

**Fully automate batch sorting** *(depends on: scan-ahead)*
Zero prompts for standard cases. Apply all rules from `Music-Library-Rules.md` automatically. Ambiguous edge cases (unknown genre, artist not in library) can still prompt - but the common path must be fully automatic. The current interactive Y/N/Q per-file flow is a stepping stone, not the end goal.

**Confidence report** *(depends on: fully automated sorting)*
After every integration run, produce a report so the user can verify the batch with zero manual checking:
- Per-file table: original filename, destination, tag changes applied, status (moved/skipped/error)
- Before/after counts: files in NewMusic before vs. moved successfully - mismatch is a hard error, not a warning
- Destination sanity check: re-read each destination file, confirm readable and tags intact
- New folders listed explicitly - any newly created folder called out so the user can spot misroutes
- Errors surfaced clearly - run does not silently continue past failures
- Save to `logs/integration-YYYYMMDD.txt` AND print to terminal
- LibChecker runs immediately after as a second validation layer

---

### AudioMirror Integration

**Auto-commit and push after regeneration**
After AudioManager regenerates the AudioMirror XML files, automatically commit and push the AudioMirror repo. No manual GitHub Desktop step. Commit message format follows existing convention (e.g. `"Apr 7 Update"`).

Rules:
- Do NOT commit if LibChecker reports any issues - fix first, then commit
- Only commit if files actually changed (avoid empty commits)
- This keeps AudioMirror commit history clean

---

### Codebase Audit

**Deep dive: codebase audit**
Scan for bugs, improvements, architectural issues. Document findings. Do after docs are solid so issues can be evaluated against intended behaviour.

---

## Lower Priority / Future

**"My Edits" tracking**
Detect locally edited songs by comparing duration to official track (>3-4s diff = protected from overwrite).

**Parody/original song pairing detection**
Flag songs where a parody and its original are both in the library.

**Album completion detection**
Cross-reference library against Spotify/MusicBrainz - flag where 50%+ of an album is owned.

**Fuzzy artist name matching**
Handle artist name variations during routing (e.g. "The Beatles" vs "Beatles", featured artist formatting differences). Lower priority - only matters at scale.

---

## See Also

- `docs/HISTORY.md` - completed features, settled design decisions, parked ideas
- `docs/Music-Library-Rules.md` - canonical rules for library structure
- `docs/NewMusic-Integration-Plan-20260308.md` - past batch integration (March 2026 batch A)
- `docs/NewMusic-Integration-Plan-20260407.md` - past batch integration (April 2026)
- `docs/AudioMirror-Format.md` - AudioMirror XML format and repo info

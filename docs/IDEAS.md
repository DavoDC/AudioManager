# Ideas & Future Work

Single source of truth for all pending work. Settled decisions and completed features -> `HISTORY.md`.

---

## Immediate Actions

- [ ] **David: review all docs before further implementation** - `Music-Library-Rules.md`, `AudioMirror-Format.md`, and the integration plan case studies are the requirements docs for this software. Review them for accuracy and completeness before any more integrator/launcher code is written. Corrections go back into the docs first, then code follows.
- [ ] **Run LibChecker on full library** - "version" and "explicit" added to UnwantedInfo (commit 24340979), filename check added (commit a2f01004). Both likely surface new hits. Fix any found issues, then commit library + report.
- [ ] **Deep dive: audit full library against Music-Library-Rules.md** - Claude to scan AudioMirror XML files, cross-reference every track against the rules doc, and produce a report of violations and gaps. Then review LibChecker source - identify any rules from the doc that LibChecker does not currently enforce, and add the missing checks. Goal: LibChecker should be comprehensive enough that a clean run means the library fully conforms to the rules.
  - *Partial progress (2026-04-09)*: LibChecker rules gap analysis done. Added `CheckAlbumSubfolderRule()` (album subfolder rule) and `CheckGenreVsFolder()` (inverse genre check). Remaining: run LibChecker on full library to surface actual hits; scan AudioMirror XMLs for violations not caught by LibChecker.

---

## Current Goal

~~Get to the point where a new music batch can be integrated using the program rather than manually - with dry run, metadata editing, routing, validation, and a confidence report.~~

**GOAL ACHIEVED (2026-04-09).** All integration pipeline features implemented. Next goal: first real integration run using the program, then LibChecker clean run to validate the library state.

---

## Pending - Priority Order

*(Quick wins first, then by dependency order.)*

---

### Docs & Knowledge

*(All done - see HISTORY.md)*

---

### Quick Wins

*(All done - see HISTORY.md)*

---

### Launcher

**Single batch launcher** *(implemented: `scripts/launch.bat`)*
~~One `.bat` file, menu-driven. Auto-builds via MSBuild before running.~~ Done.
- Remaining: integration terminal output must NOT bleed into the analysis report (already handled by TeeWriter in analysis mode; integrate mode doesn't use capture).

**Dry run mode** *(implemented: `--dry-run` flag)*
~~MusicIntegrator prints every planned action without executing.~~ Done - invoked via launcher option 3 or `--dry-run` flag.
- Remaining: tag changes and renames are not yet implemented (only moves), so dry-run covers what integration currently does.

---

### Integration Pipeline

*(In dependency order - implement top to bottom.)*

**Metadata editing before move** *(implemented)*
- TCMP=True set on every incoming track via `PreProcessTags()`
- Akira The Don genre set to Musivation via `PreProcessTags()`
- Non-MP3 files already filtered by `*.mp3` glob in Directory.GetFiles

**Integration log** *(implemented)*
`SaveLog()` writes `logs/integration-YYYYMMDD.txt` (or `-dryrun.txt`) per run.
Per-file: filename, artists, title, album, destination, tag changes, status (moved/skipped/error).
Summary: total in NewMusic, moved count, skipped count.
Logs folder is gitignored.

**Scan-ahead: 3-song threshold routing** *(implemented - routing only)*
`RunScanAhead()` pre-scans batch tags + AudioMirror Misc XMLs to find artists hitting 3+.
Those artists get routed to `Artists/{artist}/` instead of Misc - shown in reason as "[new via scan-ahead]".
Preview printed before the per-file loop.

Remaining: existing Misc songs for those artists still need MANUAL migration (flagged in preview).
The auto-migration of existing Misc songs is not implemented - it involves moving existing library files
which is a high-risk operation that needs a separate confirmation step.

**Fully automate batch sorting** *(implemented)*
Standard routes (Musivation, Motivation, Artists/ folder) are auto-accepted with `[AUTO]` label.
Only Misc routing prompts the user - it's ambiguous (artist may belong elsewhere).
Prompt changes from "[Y] Accept  [N]..." to "[Y] Accept (Misc)  [N]..." to make it clear why you're being asked.

**Confidence report** *(implemented)*
`PrintConfidenceReport()` prints after every run:
- Count check: NewMusic total vs moved vs skipped (mismatch = hard error)
- Per-file table: filename, status, destination, tag changes applied
- New folders: any folder with exactly 1 file after the run (likely newly created)
- Destination sanity check: re-reads each moved file to confirm exists and readable
- Error summary: any files that failed

Remaining: LibChecker auto-run as second validation layer after integration is not implemented.
(Analysis mode re-runs LibChecker fully; integrate mode doesn't currently trigger it.)

---

### AudioMirror Integration

**Auto-commit and push after regeneration** *(implemented)*
`AudioMirrorCommitter.TryCommit()` runs after every clean analysis run.
- Skips if LibChecker had any hits
- Skips if nothing changed (git status --porcelain is empty)
- Commits staged AUDIO_MIRROR/ with "MMM d Update" message and pushes
- LibChecker now exposes `IsClean` property; prints "LibChecker: Clean" when zero hits

---

### Codebase Audit

**Deep dive: codebase audit** *(done 2026-04-09)*
Issues found and fixed:
- Analyser library size counted non-MP3 files (fixed: `*.mp3` filter)
- LibChecker missing Sources/Films and Sources/Shows OST check (fixed: `CheckSourcesFolder()`)
- MusicIntegrator TagLib resource leak (fixed: `using` block)
- MusicIntegrator routing: "no distinct album" went to artist root (fixed: routes to `Singles/`)
- AgeChecker early-return path skips `FinishAndPrintTimeTaken()` (minor, not fixed - timing only)

---

### AudioMirror as primary scan target (already implemented)

AudioMirror XML files are the source of truth for all analysis and LibChecker runs - the actual audio files are never touched during analysis. This is intentional and correct: AudioMirror is safer (no risk of corrupting audio files), faster (XML reads vs audio file I/O), and version-controlled (XML diffs show what changed). Any future analysis tools should read from AudioMirror XML, not from the audio files directly.

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

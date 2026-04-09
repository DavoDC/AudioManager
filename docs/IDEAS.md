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

**Auto-commit and push after regeneration** *(implemented)*
`AudioMirrorCommitter.TryCommit()` runs after every clean analysis run.
- Skips if LibChecker had any hits
- Skips if nothing changed (git status --porcelain is empty)
- Commits staged AUDIO_MIRROR/ with "MMM d Update" message and pushes
- LibChecker now exposes `IsClean` property; prints "LibChecker: Clean" when zero hits

---

### Codebase Audit

**Deep dive: codebase audit**
Scan for bugs, improvements, architectural issues. Document findings. Do after docs are solid so issues can be evaluated against intended behaviour.

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

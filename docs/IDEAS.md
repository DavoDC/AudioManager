# Ideas & Future Work

Single source of truth for all pending work. Settled decisions and completed features -> `HISTORY.md`.

---

## Immediate Actions

- [ ] **Run LibChecker on full library** - "version" and "explicit" added to UnwantedInfo (commit 24340979), likely surfaces new hits. Fix any found issues, then commit library + report.

---

## Current Goal

Get to the point where a new music batch can be integrated using the program rather than manually - with dry run, metadata editing, routing, validation, and a confidence report.

---

## Open Decisions

**[DECIDED] Integrator stays in AudioManager** - The integrator shares Constants, LibChecker, and tag models with the rest of AudioManager. Splitting it out would require duplicating or packaging shared code with no real gain at this scale. Keep unified - logical separation (each has its own entry point) is enough.

**[DECIDED] Audio reports stay in AudioManager** - AudioMirror is a data repo. Generating/storing analysis reports there would blur its purpose. AudioManager owns the tools and the outputs.

**[OPEN] LibChecker output in audio report** - "Checking library..." process lines don't belong in a stats report. Proposal: if LibChecker clean, show a single `LibChecker: Clean` line. If issues, prominently flag them and block the AudioMirror commit. Document this rule once decided.

---

## Pending - Priority Order

*(Quick wins first, then by dependency order.)*

---

### Docs & Knowledge

Do these before writing more integrator code - rules must be correct before automating them.

**Review and improve all docs** *(start here)*
Go through all docs in `docs/`, fix inaccuracies, clarify ambiguities, answer open questions. Goal: any future session should be able to pick up the project cold from docs alone.

**Refine NewMusic-Integration-Plan-20260407b.md**
Open notes inside that doc need resolving. See file for detail.

**Fix date on NewMusic-Integration-Plan-20260407.md**
The date is wrong - that integration was done earlier than Apr 7 2026. Check AudioMirror commit `b8e15b11923e0b1c0bbcca1563e45b1e9eafa8ea` to find the real date and correct the filename and doc header.

**Review past integration docs for undocumented rules**
Scan AudioMirror XML metadata and commit history. Look for routing decisions not yet captured in `Music-Library-Rules.md`. Rules doc must be comprehensive before the integrator can fully automate them.

**Extract Word doc into Music-Library-Rules.md**
The Word doc below is the original process source. Extract all rules into `docs/Music-Library-Rules.md` so the Word doc is no longer the source of truth. Extracted content is pasted at the bottom of this file for convenience.

---

### Quick Wins

*(none remaining - see HISTORY.md)*

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

## Reference: Word Doc Content (for extraction into Music-Library-Rules.md)

Source: `C:\Users\David\GoogleDrive\Documents\WordDocs\Audio Folder Organisation Usage Process .docx`
Extract this into `docs/Music-Library-Rules.md`, then this section can be removed.

### Process of Getting New Music

**Stage 1: Acquire**
1. Discover on Spotify via release radar etc. - add to liked songs.
2. For each new song, also check that artist out - look at their top 10 streamed songs, look for other things you like.
3. Download from relevant place - remove from liked songs.

**Stage 2: Integrate**
1. Apply rules using Mp3tag in Downloads folder.
2. Integrate music into library following organisational rules below.
3. Run AudioManager program to check library, commit update, commit audio report and AudioMirror changes.

**Stage 3: Sync to Device** *(cannot automate - prompt user)*
1. Open iTunes and ensure device is detected.
2. Add Audio folder to iTunes.
3. Use File -> Library -> Show Duplicate Items, and remove duplicates.
4. Check for broken files (exclamation symbol on far left).
5. Sync device twice to get new music.

### Organisational Rules

**Global Rules**
- All files should be MP3 format
  - To convert many: Format Factory -> To MP3 -> Add files -> Output to Source file folder
  - Search in Explorer for `NOT *.mp3 AND NOT kind:folder`
- Use MP3Tag's tag-to-filename feature: `%artist% - %title%`
- Tags must include at minimum: Title, Artist, Album, Year, Cover
  - If no album, use Title as Album
- Remove from all fields: "feat.", "ft.", "Edit", "Version", "Original", "Soundtrack"
- Set all tracks as compilations (TCMP=1) - stops iTunes listing each track as a separate album

**Folder: Artists**
- Artists who have 3 or more songs
- Put album names within subfolders; remainder goes into Singles
- No loose files

**Folder: Compilations**
- Compilation albums

**Folder: Musivation**
- All songs should have Musivation genre

**Folder: Miscellaneous Songs**
- Random singles
- Try to move into Artists (look for trios or misplaced)
- Try to move into Sources (look for movie soundtracks etc.)

**Folder: Sources**
- Tracks discovered through a specific form of media
- Films:
  - Change "Original Motion Picture Soundtrack" to OST
  - All should have an album field containing "OST"
- Anime songs:
  - Use English titles for memorability
  - Use Anime name as Album
  - Full length for good ones, anime length for lesser ones
  - Replace cover art with Anime cover

---

## See Also

- `docs/HISTORY.md` - completed features, settled design decisions, parked ideas
- `docs/Music-Library-Rules.md` - canonical rules for library structure
- `docs/NewMusic-Integration-Plan-20260407.md` - past batch integration (April 2026 batch A) *[date needs fixing]*
- `docs/NewMusic-Integration-Plan-20260407b.md` - past batch integration (April 2026 batch B) *[complete, notes inside need resolving]*
- `docs/AudioMirror-Format.md` - AudioMirror XML format and repo info

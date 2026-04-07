# Ideas & Future Work

Single source of truth for all pending work. Settled decisions and completed features -> `HISTORY.md`.

---

## Current Focus

**Goal:** integrate new music from `C:\Users\David\Downloads\NewMusic` into library at `C:\Users\David\Audio`.

The integrator is working (reads tags, auto-routes, interactive Y/N/Q per file, manual folder picker). Before running on the real library, two things must be in place: metadata editing (Akira genre fix, IsCompilation tag) and a JSON integration log so moves can be reviewed. Constants.cs and LibChecker are quick wins to clean up first.

---

## Pending - Main Work

*(Ordered by priority. All items below are required for the integration directive.)*

---

FIRST task is a meta task is to go over this entire doc and turn these mnaully added tasks into proper tasks! and sort by quick win/priority.
also need to check workspace repo for any audiomanager tasks and move into here. i think there was as task about possibly splitting integrator out.  maybe intergrator should be spearate project as its quite different from of project?  or good to stay here and benefits from shared code?  claude to investigate!   also might to split some of these things i manually added into separate tasks

more ideas= review all docs and fix issues, ask questions ,  need to build up better docs and understand so claude cna work better with this proejct

need to update goal = goal is that when next integraito happens, can use a program to help with it rather than doing manually! 

maybe audioamanger is doing too much ,maybe it should be split out ... get claude to think about all the things it does, mirror, analysis, etc.  could certain parts be split out,  what parts make sense to be together , maybe good to keep all unified here. 

highest piorities = docs , launching, then new features n stuff 
need more planning first abouit overarching goals, organisation, before extending project 

---

Quick task: 
Go over C:\Users\David\GitHubRepos\AudioManager\docs\NewMusic-Integration-Plan-20260407b.md and refine as per notes inside it!

---

Consider if Checking library..., lib checker otuput should be removed from audio report coz should just be stats 
but maybe good to ahve clean lib checker note there or issue note.  iF issues DO NOT commit report and DO not commit to audiomirror repo, fix issues first, then commit to both repos! document this rule somewhere and follow , maybe cluade.md and others 

---

b8e15b11923e0b1c0bbcca1563e45b1e9eafa8ea = audio mirror commit that i think corresponds to NewMusic-Integration-Plan-20260407 , NEED TO FIX DATE ON THAT DOC coz it is wrong!  THAT integraiton was done long time ago!

---

review past integration docs against music library rules , music libnrary rules should be fully comprehensive. need to perfect rules doc before writing integrator to automate!!!!!!!!!!
---

**Single batch launcher** *(quick win)*

very high priority to get the launching eaiser and make it so dont have to open visual studio!  need to auto build tho 

Main entry point for the program. One `.bat` with a mode menu:
1. Dry run integration - show all planned moves/tag changes, no files touched
2. Real integration - execute moves and tag changes
3. Analysis only - run Analyser, save report, skip integration

Could have top level menu instead of sub menus or vice versae. e.g. top level menus = Analysis (No Force Regen), Analysis (Froce Regen), Integration (Dry Run), Integration

Rules for all modes:
- Auto-compile via MSBuild before running
- Show output in terminal, log to file, `cmd /k`
- Analysis always runs and saves report to file; integration terminal output must NOT bleed into the report
- If NewMusic folder is empty or doesn't exist in integration modes, skip silently

Dry run is the data-safety gate - must be run before every real integration. See dry run mode idea below for implementation detail.

---

**Dry run mode**

MusicIntegrator prints every planned action (tag change, rename, move) without executing any of them. Invoked via the launcher menu (option 1) or a `--dry-run` flag.

This is a data-safety gate - the music library is the primary copy and not frequently backed up. No batch should ever be run for real without a dry run first.

---

**Constants.cs consolidation** *(quick win)*

Single source of truth at project level (not inside Code/ folder):
- Extract `miscDir`, `artistsDir`, `musivDir`, `motivDir` from anywhere they're duplicated
- LibChecker and MusicIntegrator must both read from Constants, never define their own copies

---

Get claude to scan over audiomirror xml metadata and commit history and understand how ive been organising library , maybe there are other rules not in doc

---

claude to deep dive this codebase look for improvements, issues, bugs 

---

**LibChecker: detect version/edition suffixes in title** *(quick win)*

Titles should not contain suffixes like `(Explicit Version)`, `(Album Version)`, `(Radio Edit)`, `(Deluxe Edition)` etc. LibChecker currently misses these - caught manually when `Bow Wow;Chris Brown - Ain't Thinkin' 'Bout You (Explicit Version)` was integrated (Apr 2026). Add a check that flags titles containing known version/edition keywords.

---

**LibChecker owns validation** *(quick win)*

MusicIntegrator must not duplicate LibChecker logic:
- MusicIntegrator handles routing/moving only
- LibChecker runs after and validates the result

---

**Metadata editing before move**

Before accepting a file move, apply metadata changes:
- Set genre to `Musivation` for Akira The Don tracks
- Set `IsCompilation = true` (TCMP tag) via ID3v2 on every file before moving
- Only process `.mp3` files - filter other extensions out

---

**JSON integration log** *(depends on metadata editing above)*

Append to `MusicIntegrationLog.json` per run:
- Include full metadata fields and decision fields (source, destination, tag changes made, timestamp)
- Decision history principle: on re-run, check log first before re-deciding

---

**Scan-ahead: 3-song threshold and Misc migration**

Before processing a batch, scan all files to calculate post-integration artist counts. This is required to correctly apply the routing rules:
- Identify artists in the batch that will hit the 3+ song threshold -> new artist folder needed
- Identify existing Misc songs by those artists -> migrate them to the new folder at the same time
- Show a preview of all planned moves before executing anything

Without this, files get routed to Misc incorrectly for artists that should get their own folder.

---

**Fully automate new-music batch sorting**

The integrator should automatically sort every file in the NewMusic inbox to the correct destination with no prompts, applying all rules in `Music-Library-Rules.md` (routing priority, album subfolder rule, artist threshold, Akira/Loot special cases). The interactive Y/N/Q per-file flow is a stepping stone - the end goal is zero manual decisions for a standard batch. Any ambiguous edge cases (genuinely unknown genre, artist not in library) can still prompt, but the common path must be fully automatic.

---

**Confidence report: user must be certain nothing was lost or misrouted**

After every integration run the integrator must produce a confidence report that lets the user verify the batch with zero manual checking. Design goals:

- Every source file accounted for - show a per-file table: original filename, destination path, tag changes applied, status (moved / skipped / error)
- Before/after counts - total files in NewMusic inbox before vs. files moved successfully; any mismatch is a hard error, not a warning
- Destination sanity check - after moving, re-read each destination file and confirm it is readable and tags are intact (not corrupted in transit)
- New folders listed explicitly - any newly created artist/album/category folder is called out so the user can spot misroutes at a glance
- Errors surfaced clearly - any file that could not be moved or tagged is shown with the reason; run does not silently continue
- Dry run matches real run - dry run output format must be identical to the real run report (minus "moved" status), so the user can diff them

The report should be saved to a timestamped file (e.g. `logs/integration-YYYYMMDD.txt`) AND printed to terminal. LibChecker then runs immediately after as a second layer of validation.

---

## Lower Priority / Future

**Integrate audio organisation Word doc into codebase**

`C:\Users\David\GoogleDrive\Documents\WordDocs\Audio Folder Organisation Usage Process .docx` contains rules/process for the audio library. Extract and integrate its content into `docs/Music-Library-Rules.md` so all routing logic is in one place and the Word doc is no longer the source of truth.
HERE IS DOC FOR PROCESSING:
Process of Getting New Music
Stage 1: Acquire
1.	Discover on Spotify via release radar etc.  – add to liked songs.
2.	Download from relevant place... – remove from liked songs. 

Actually more happens here  - for each new song i find i also check that artist out, look at their top 10 streamed songs, look for toehr things i like,  maybe GUI dwave could help with this...


Stage 2: Integrate
1.	Apply some rules using Mp3tag in Downloads folder.
2.	Integrate music into library following organisational rules below.
3.	Run AudioManager program to check library and commit update , commit audio report and audio miror changes etc. 

Stage 3: Sync to Device = CANT AUTOMATE THIS BUT tell user to do this 
1.	Open iTunes and ensure device is detected.
2.	Add Audio folder to iTunes.
3.	Use File -> Library -> Show Duplicate Items, and remove duplicates.
4.	Check for broken files (exclamation symbol on far left).
5.	Sync device twice to get new music.

Organisational Rules
Note: Many of these rules are automatically checked by the AudioMirror project.
Global Rules
•	All files should be MP3 format
o	For doing many, open Format Factory -> To MP3 -> Add files
o	Search in Explorer for 'NOT *.mp3 AND NOT kind:folder'
o	Output to Source file folder.
•	Use MP3Tag’s tag to file name feature to name files: 
o	%artist% - %title% 
•	Tag should include Title, Artist, Album, Year, and a Cover, at least, ideally
o	If no Album, use Title.
•	Remove from all fields:
o	"feat." or “ft.”
o	"Edit”
o	“Version”
o	“Original"
o	“Soundtrack”
•	Set all tracks as compilations
o	Stops iTunes from listing each track as a separate album due to having different artists

Folder: Artists
	Artists who have 3 or more songs.
	Put Album names within folders, remainder goes into Singles.
	No loose files!

Folder: Compilations
	Compilation albums.

Folder: Musivation
	All songs should have Musivation genre.

Folder: Miscellaneous Songs
	Random singles.
	Try to move into Artists (look for trios or misplaced).
	Try to move into Sources (look for movie soundtracks etc.).

Folder: Sources
	This for any tracks you discovered through a certain form of media.
	Films
o	Change "Original Motion Picture Soundtrack" to OST.
o	All should have an album field containing “OST”.
	Anime songs
o	Use English titles for memorability.
o	Use Anime name as Album.
o	Full length for good ones, anime length for lesser ones.
o	Replace cover art with Anime cover.


---

**AudioMirror: auto-commit and push after regeneration**

After AudioManager regenerates the AudioMirror XML files, automatically commit and push the AudioMirror repo. No manual GitHub Desktop step needed. Use `git add -u && git add untracked && git commit && git push` via Process/shell call from AudioManager after regeneration completes. Commit message format follows existing convention: `"Apr 7 Update"` style.
BUT commit should not happen if there are libchecker issues!!!!!!!!!!!  try to minimise commits to audiomirror to make commit history clean.

---

claude do u think audio reports should in audiomanager or audio mirror ?  good that audio mirror is separate?  i split it off a while back, used to be together 

---


*(Ordered by size - smaller first.)*

---

**"My Edits" tracking**

Detect locally edited songs by comparing duration to official track (>3-4s diff = protected from overwrite).

---

**Parody/original song pairing detection**

Flag songs where a parody and its original are both in the library.

---

**Album completion detection**

Cross-reference library against Spotify/MusicBrainz - flag where 50%+ of an album is owned.

---

**Fuzzy artist name matching**

Handle artist name variations during routing (e.g. "The Beatles" vs "Beatles", featured artist formatting differences). Lower priority - only matters at scale.

---

## See Also

- `docs/HISTORY.md` - completed features, settled design decisions, parked ideas
- `docs/Music-Library-Rules.md` - canonical rules for library structure
- `docs/NewMusic-Integration-Plan-20260407.md` - past batch integration (April 2026 batch A)
- `docs/NewMusic-Integration-Plan-20260407b.md` - past batch integration (April 2026 batch B)
- `docs/AudioMirror-Format.md` - AudioMirror XML format and repo info

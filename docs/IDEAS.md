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

**Dry run mode** *(quick win)*

Add a `--dry-run` flag to MusicIntegrator that prints every planned action (tag change, rename, move) without executing any of them. Must be the default for first-time runs against a new batch. Real execution requires an explicit flag or confirmation prompt.

This is a data-safety gate - the music library is the primary copy and not frequently backed up. No batch should ever be run for real without a dry run first.

---

**Constants.cs consolidation** *(quick win)*

Single source of truth at project level (not inside Code/ folder):
- Extract `miscDir`, `artistsDir`, `musivDir`, `motivDir` from anywhere they're duplicated
- LibChecker and MusicIntegrator must both read from Constants, never define their own copies

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

**Single batch launcher** *(quick win)*

One `.bat` that always runs both analysis and integration:
- Analysis always runs; saves report to file (integration terminal output must NOT be included in the report)
- Integration runs after analysis; if NewMusic folder is empty or doesn't exist, skip silently - no prompt, no error
- Show output in terminal, log to file, `cmd /k`, auto-compile via MSBuild before running

---

## Lower Priority / Future

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
- `docs/NewMusic-Integration-Plan.md` - past batch integration (April 2026 case study)
- `docs/AudioMirror-Format.md` - AudioMirror XML format and repo info

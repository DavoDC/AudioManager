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

**Single batch launcher** *(quick win)*

One `.bat` that always runs both analysis and integration:
- Analysis always runs; saves report to file (integration terminal output must NOT be included in the report)
- Integration runs after analysis; if NewMusic folder is empty or doesn't exist, skip silently - no prompt, no error
- Show output in terminal, log to file, `cmd /k`, auto-compile via MSBuild before running

---

## Lower Priority / Future

*(Ordered by size - smaller first.)*

---

**ReportWriter - plain static class** *(quick win)*

Should be a plain static class, NOT inheriting from the Doer base class.

---

**Analyser class stats**

- Playback hours (total and average)
- Song length metrics (shortest, longest, mean)
- Library size (track count, total size in MB/GB)
- Age stats (oldest track, newest track, year distribution)

---

**Smart merge / scan-ahead logic** *(largest item)*

Before integrating a batch, calculate post-integration artist counts:
- Determine if new artist folders need creating (3+ song threshold)
- Determine if existing misc songs should migrate to the new artist folder
- Show preview/approval step before committing any moves
- Fuzzy matching for artist name variations (e.g. "The Beatles" vs "Beatles")
- Handle featured artists correctly in routing decisions

---

**Fully automate new-music batch sorting**

The integrator should automatically sort every file in the NewMusic inbox to the correct destination with no prompts, applying all rules in `Music-Library-Rules.md` (routing priority, album subfolder rule, artist threshold, Akira/Loot special cases). The interactive Y/N/Q per-file flow is a stepping stone - the end goal is zero manual decisions for a standard batch. Any ambiguous edge cases (new artist below threshold, unknown genre) can still prompt, but the common path must be fully automatic.

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

## See Also

- `docs/HISTORY.md` - completed features, settled design decisions, parked ideas
- `docs/Music-Library-Rules.md` - canonical rules for library structure
- `docs/NewMusic-Integration-Plan.md` - past batch integration (April 2026 case study)
- `docs/AudioMirror-Format.md` - AudioMirror XML format and repo info

# Ideas & Future Work

Single source of truth for all pending work. Settled decisions and completed features -> `HISTORY.md`.

---

## Current Focus

**Goal:** integrate new music from `C:\Users\David\Downloads\NewMusic` into library at `C:\Users\David\Audio`.

Stage 1 integrator is working (reads tags, auto-routes, interactive Y/N/Q per file, manual folder picker). Before running on the real library, the priority pending items are: Stage 2 (set IsCompilation tag, Akira genre fix), Stage 3 (integration log so moves can be reviewed). Constants.cs / LibChecker / ReportWriter cleanups are quick wins to do first.

---

## Pending - Main Work

*(Ordered by priority. Quick wins first.)*

---

**ReportWriter - plain static class**

Should be a plain static class, NOT inheriting from the Doer base class.

---

**Constants.cs consolidation**

Single source of truth at project level (not inside Code/ folder):
- Extract `miscDir`, `artistsDir`, `musivDir`, `motivDir` from anywhere they're duplicated
- LibChecker and MusicIntegrator must both read from Constants, never define their own copies

---

**LibChecker owns validation**

MusicIntegrator must not duplicate LibChecker logic:
- MusicIntegrator handles routing/moving only
- LibChecker runs after and validates the result

---

**Single batch launcher**

One `.bat` that always runs both analysis and integration:
- Analysis always runs; saves report to file (integration terminal output must NOT be included in the report)
- Integration runs after analysis; if NewMusic folder is empty or doesn't exist, skip silently - no prompt, no error
- Show output in terminal, log to file, `cmd /k`, auto-compile via MSBuild before running

---

**Stage 2 - Metadata editing before move**

Before accepting a file move, apply metadata changes:
- Set genre to `Musivation` for Akira The Don tracks
- Set `IsCompilation = true` (TCMP tag) via ID3v2 on every file before moving
- Only process `.mp3` files - filter other extensions out

---

**Stage 3 - JSON integration log** *(depends on Stage 2)*

Append to `MusicIntegrationLog.json` per run:
- Include full metadata fields and decision fields (source, destination, tag changes made, timestamp)
- Decision history principle: on re-run, check log first before re-deciding

---

**Analyser class stats**

- Playback hours (total and average)
- Song length metrics (shortest, longest, mean)
- Library size (track count, total size in MB/GB)
- Age stats (oldest track, newest track, year distribution)

---

**Smart merge / scan-ahead logic** *(depends on Stage 2 + 3, largest item)*

Before integrating a batch, calculate post-integration artist counts:
- Determine if new artist folders need creating (3+ song threshold)
- Determine if existing misc songs should migrate to the new artist folder
- Show preview/approval step before committing any moves
- Fuzzy matching for artist name variations (e.g. "The Beatles" vs "Beatles")
- Handle featured artists correctly in routing decisions

---

## Lower Priority / Future

*(Ordered by size - smaller first.)*

---

**Document and automate the full new-music integration workflow**

Same approach as RivalsVidMaker - document the entire manual workflow end-to-end first, then look for automation opportunities, then gradually automate more and more of the pipeline. Goal: run one tool, new music ends up in the right place with correct tags, zero manual steps.

---

*(DWave features - full plan in P10 directive.)*

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

- `HISTORY.md` - completed features, settled design decisions, parked ideas *(create when first item is completed)*
- `docs/music-library-rules.md` - canonical rules for library structure
- `docs/NewMusic integration plan.md` - integration pipeline design notes

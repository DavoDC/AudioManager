# Ideas & Future Work

Single source of truth for all pending work. Settled decisions and completed features -> `HISTORY.md`.

---

## Current Focus

MusicIntegrator pipeline - Stage 2 (metadata editing) and Stage 3 (integration log) are next, then smart merge logic.

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

**Separate batch launchers**

- One `.bat` for Analysis mode (scan + report + mirror)
- One `.bat` for Integration mode (validate tags + move + update mirror)
- Both: show output in terminal, log to file, `cmd /k`, auto-compile via MSBuild before running

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

*(DWave features - full plan in P10 directive. Ordered by size - smaller first.)*

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
- `Docs/music-library-rules.md` - canonical rules for library structure
- `Docs/NewMusic integration plan.md` - integration pipeline design notes

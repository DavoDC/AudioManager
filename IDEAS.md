# AudioManager IDEAS / TTD

## Pending

### MusicIntegrator Stages

1. **Stage 2 - Metadata editing before move** - before accepting a file move, apply metadata changes:
   - Set genre to `Musivation` for Akira The Don tracks
   - Set `IsCompilation = true` (TCMP tag) via ID3v2 on every file before moving
   - Only process `.mp3` files - filter other extensions out

2. **Stage 3 - JSON integration log** - append to `MusicIntegrationLog.json` per run:
   - Include full metadata fields and decision fields (source, destination, tag changes made, timestamp)
   - Decision history principle: on re-run, check log first before re-deciding

3. **Smart merge / scan-ahead logic** - before integrating a batch, calculate post-integration artist counts:
   - Determine if new artist folders need creating (3+ song threshold)
   - Determine if existing misc songs should migrate to the new artist folder
   - Show preview/approval step before committing any moves
   - Fuzzy matching for artist name variations (e.g. "The Beatles" vs "Beatles")
   - Handle featured artists correctly in routing decisions

### Analyser / Stats

4. **Analyser class stats** (partially designed in earlier session):
   - Playback hours (total and average)
   - Song length metrics (shortest, longest, mean)
   - Library size (track count, total size in MB/GB)
   - Age stats (oldest track, newest track, year distribution)

### Code Structure

5. **Constants.cs consolidation** - single source of truth at project level (not inside Code/ folder):
   - Extract `miscDir`, `artistsDir`, `musivDir`, `motivDir` from anywhere they're duplicated
   - LibChecker and MusicIntegrator must both read from Constants, never define their own copies

6. **LibChecker owns validation** - MusicIntegrator must not duplicate LibChecker logic:
   - MusicIntegrator handles routing/moving only
   - LibChecker runs after and validates the result

7. **ReportWriter** - should be a plain static class, NOT inheriting from the Doer base class

### Launchers

8. **Separate batch launchers** (from P5 directive):
   - One `.bat` for Analysis mode (scan + report + mirror)
   - One `.bat` for Integration mode (validate tags + move + update mirror)
   - Both: show output in terminal, log to file, `cmd /k`, auto-compile via MSBuild before running

### Future / DWave Features (tracked here for context)

- Album completion detection - cross-reference library against Spotify/MusicBrainz (flag where 50%+ of album owned)
- "My Edits" tracking - detect locally edited songs by comparing duration to official (>3-4s diff = protected)
- Parody/original song pairing detection
- DWave full plan is in P10 directive (DWave Dashboard)

## Completed

*(nothing yet - this file created 2026-04-06 from Claude.ai export)*

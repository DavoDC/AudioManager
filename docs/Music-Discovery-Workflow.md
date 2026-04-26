# Music Discovery to Device Workflow

Complete end-to-end process: Spotify discovery → local library → phone sync.

## Workflow Overview

**Input → Stage 1 → Stage 2 → Stage 3 → Stage 4 → Output**

- **INPUT:** Spotify Release Radar + existing library
- **Stage 1 (Discovery):** Find and like new tracks on Spotify
- **Stage 2 (Acquiring):** Download liked songs to NewMusic folder  
- **Stage 3 (Integrate):** Tag, organize, and integrate into library (ensure clean library first)
- **Stage 4 (Sync):** Update iTunes and sync to device
- **OUTPUT:** Device with latest music, library clean and organized

Each stage has clear inputs and outputs. Optimize each transition to move most efficiently through the pipeline.

## Stage 1: Discovery

**Input:** Spotify Release Radar, existing library with liked artists  
**Output:** Playlist of liked songs ready for acquisition

Discover on Spotify via release radar - add to liked songs.

For each new song, check that artist out - look at top 10 streamed songs, find other tracks you like on albums already in library.

## Stage 2: Acquiring

**Input:** Liked songs playlist from Spotify  
**Output:** Downloaded MP3 files in `C:\Users\David\Downloads\NewMusic\`

1. Add all songs to a playlist and remove all songs from liked songs
2. Run `C:\Users\David\GitHubRepos\SpotifyPlaylistGen\scripts\open_playlist_in_manager` on that playlist
   - Script extracts track artists and names
   - Sends to music service app for downloading
   - Music service places MP3s in `C:\Users\David\Downloads\NewMusic\`
3. Verify all tracks downloaded successfully

> **Note:** `open_playlist_in_manager` script is rough and not fully integrated. Future: integrate seamlessly into SpotifyPlaylistGen.

## Stage 3: Integrate

**Input:** MP3 files in `C:\Users\David\Downloads\NewMusic\`, clean library state (LibChecker 0 issues)  
**Output:** Tagged, named, and organized files integrated into library; AudioMirror updated

### Pre-Integration: Achieve Clean Library
1. Fix any LibChecker blocking issues (via dev session)
2. Run AudioManager analysis to get library clean (0 LibChecker issues)
3. This ensures library is ready before adding new music

### Integration Steps
1. Run the AudioManager launcher: `scripts/launch.bat`
2. Choose `3. Integration (Dry Run)` - preview all planned changes
3. Choose `4. Integration (Real)` - execute the integration

This handles:
- Tag cleanup and compliance per Music-Library-Rules (add TCMP, set Musivation genre, remove unwanted strings)
- Filename renaming per naming convention
- Routing files to library folders (Artists, Musivation, Motivation, Compilations, Misc, or Sources with optional prompt for Films/Shows/Anime)
- Integration into library
- Analysis and commit of results to AudioMirror repo

Result: fully automated, no manual Mp3tag or separate analysis step needed.

## Stage 4: Sync to Device

*(Manual - cannot automate)*

**Input:** Integrated library with new music in Audio folder  
**Output:** Device synchronized with latest library

### Update iTunes Library
1. Add Audio folder to iTunes
2. File → Library → Show Duplicate Items → remove duplicates
3. Check for broken files (exclamation symbol on far left)

### Sync Device
1. Open iTunes and ensure device is detected
2. Sync device twice to pick up new music

---

## Reference

See **Music-Library-Rules.md** for:
- Library folder structure
- Tag format and rules
- Track routing logic
- Folder-specific rules (Musivation, Sources, etc.)
- Known gotchas

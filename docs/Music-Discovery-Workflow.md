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

**Input:** MP3 files in `C:\Users\David\Downloads\NewMusic\`, library ready for integration  
**Output:** Tagged, named, and organized files integrated into library; AudioMirror updated

### Integration Launcher
Run the AudioManager launcher: `scripts/launch.bat`
- Choose `3. Integration (Dry Run)` - preview all planned changes
- Choose `4. Integration (Real)` - execute the integration

### Currently Implemented (TIER 0/1)
- ✓ **Filename renaming:** Per naming convention
- ✓ **Routing:** Artist songs to existing folders or with 3+ threshold scan-ahead. Falls to Misc for unknown artists. Sources/Films/Shows/Anime: interactive folder-picker when needed.
- ✓ **File movement:** Integrates into library folder structure
- ✓ **Post-integration validation:** Run LibChecker to verify clean state
- ✓ **Commit:** Results to AudioMirror repo
- ✓ **Pre-integration duplicate check:** Warns if song already in library (TIER 0)

### Planned/In Progress (TIER 1)
- 🔄 **Tag cleanup auto-removal:** Strip unwanted text (feat., version, explicit) from tags before integration (TIER 1, currently manual via Mp3tag)
- 🔄 **Pre-integration LibChecker gate:** Check library is clean before allowing integration (TIER 0 blocking)

### Full Pipeline Vision
Once all features complete: one command (`AudioManager --integrate`), zero manual Mp3tag or separate analysis steps.

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

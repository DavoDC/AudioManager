# Music Discovery to Device Workflow

Complete end-to-end process: Spotify discovery → local library → phone sync.

> **Scope:** This document is user-facing workflow only. It describes WHAT to do and in WHAT ORDER. Implementation details (TagFixer architecture, routing algorithm, internal validation) belong in CLAUDE.md and Music-Library-Rules.md. See `feedback_doc_scope_discipline.md` for the maintenance rule.

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
2. Run `C:\Users\David\GitHubRepos\SpotifyPlaylistGen\scripts\open_playlist.bat` on that playlist
   - Music service places MP3s in `C:\Users\David\Downloads\NewMusic\`
3. Verify all tracks downloaded successfully

## Stage 3: Integrate

**Input:** MP3 files in `C:\Users\David\Downloads\NewMusic\` (tags cleaned manually in MP3Tag)  
**Output:** Tagged, named, and organized files integrated into library; AudioMirror updated

**Steps:**

1. **Dry-run preview:** Launch AudioManager and select option 3 (Integration - Dry Run)
   - Shows all tag fixes and routing decisions before applying
   - Review the decisions, verify they're correct
   
2. **Real integration:** Launch AudioManager and select option 4 (Integration - Real)
   - Applies tag fixes to NewMusic files
   - Routes files into library folders per Music-Library-Rules.md
   - Auto-validates via LibChecker; reports if any issues found

3. **Commit AudioMirror changes:**
   - Open GitHub Desktop
   - Stage all AUDIO_MIRROR changes
   - Commit with message like "May 25 Update"
   - (Do not push - user only, local commit)

**Reference:** See `Music-Library-Rules.md` for tag rules and routing logic. See `CLAUDE.md` for integration architecture details.

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

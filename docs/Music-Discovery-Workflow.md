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

### Critical Design: Tag Fixing is Separate from Integration

**Two distinct steps (in order):**

1. **TagFixer** (automatic tag cleanup) - reads raw NewMusic files, cleans all tags, renames files
   - Removes unwanted words from Title/Album tags: "(feat. ...)", "(Album Version)", "(Explicit)", etc.
   - Ensures featured artists are in TPE1 tag as semicolon-separated list
   - Renames files to `{artist} - {title}.mp3` format
   - Sets TCMP=1 on all files
   - Sets genre for Musivation/Motivation tracks
   - **Result:** clean, ready-to-route files

2. **Integration** (routing only) - moves cleaned files to correct library folders
   - Assumes all tags are already clean
   - Purely routes: artist songs to Artists/, genre songs to Musivation/Motivation/Compilations/, everything else to Misc
   - Should NEVER touch tags - only move files

**Why this separation?** Integration is simple, safe, and testable when it only does routing. All tag work is isolated in TagFixer. If integration fails, it's a routing issue, not a tag issue.

### Full Pipeline Sequence

1. **Tag cleanup:** Run TagFixer (not yet implemented - see IDEAS.md TIER 0)
2. **Dry run:** Run Integration (Dry Run) to preview all routing decisions
3. **Real integration:** Run Integration (Real) to move files
4. **Validation:** Post-integration LibChecker validation runs automatically
5. **Commit:** Manually commit AudioMirror changes in GitHub Desktop

### Integration Launcher (when TagFixer exists)

```bash
# Step 1: Clean tags on all NewMusic files (not yet implemented)
AudioManager tagfix

# Step 2: Preview all routing decisions
C:\Users\David\GitHubRepos\AudioManager\scripts\launch.bat
# Choose: 3. Integration (Dry Run)

# Step 3: Execute real integration
# Choose: 4. Integration (Real)

# Step 4: Commit results (manual for now)
# GitHub Desktop: Stage AUDIO_MIRROR changes, commit "Apr 27 Update"
```

### Currently Implemented

**TagFixer (tag cleanup step):**
- ❌ **NOT YET IMPLEMENTED** - Currently you must clean tags manually in MP3Tag before integration (TIER 0 BLOCKER)

**Integration (routing step):**
- ✓ **Routing logic:** Artist songs to existing folders or with 3+ threshold scan-ahead. Falls to Misc for unknown artists. Sources/Films/Shows/Anime: interactive folder-picker when needed.
- ✓ **File movement:** Moves cleaned files into library folder structure
- ✓ **Pre-integration gate:** Checks library is clean and AudioMirror is fresh before allowing integration
- ✓ **Pre-integration duplicate check:** Warns if song already in library (TIER 0)
- ✓ **Post-integration validation:** Auto-runs LibChecker to verify clean state
- ✓ **Dry-run mode:** Preview all routing decisions without moving files
- ✓ **Commit instructions:** Shows manual commit instructions (auto-commit disabled for safety)

### Planned/In Progress (TIER 0-1)
- 🔄 **TagFixer module:** Strip unwanted text from tags, rename files, ensure clean tags before integration (TIER 0 BLOCKER - blocking real integration)
- 🔄 **Full pipeline:** Once TagFixer implemented, full automated pipeline: user runs TagFixer → Integration → validation, all tags/routing automated

### Full Pipeline Vision (when TagFixer implemented)
Once TagFixer is complete: one command sequence (`AudioManager tagfix` → `AudioManager integrate`), zero manual MP3Tag work. Integration will assume clean tags and purely route.

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

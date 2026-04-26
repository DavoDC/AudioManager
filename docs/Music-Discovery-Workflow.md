# Music Discovery to Device Workflow

Complete end-to-end process: Spotify discovery → local library → phone sync.

## Stage 1: Discovery

Discover on Spotify via release radar - add to liked songs.

For each new song, check that artist out - look at top 10 streamed songs, find other tracks you like.

## Stage 2: Acquiring

1. Add all songs to a playlist and remove all songs from liked songs
2. Run `C:\Users\David\GitHubRepos\SpotifyPlaylistGen\scripts\open_playlist_in_manager` on that playlist
3. This script opens each track in a special service in browser, then places songs in `C:\Users\David\Downloads\NewMusic\`

> **Note:** `open_playlist_in_manager` script is rough and not fully integrated. Future: integrate seamlessly into SpotifyPlaylistGen.

## Stage 3: Integrate

Run the AudioManager launcher:
1. `scripts/launch.bat`
2. Choose `3. Integration (Dry Run)` - preview all planned changes
3. Choose `4. Integration (Real)` - execute the integration

This one command handles:
- Tag cleanup and compliance per Music-Library-Rules (add TCMP, set Musivation genre, remove unwanted strings)
- Filename renaming per naming convention
- Routing files to library folders (Artists, Musivation, Motivation, Compilations, Misc, or Sources with optional prompt for Films/Shows/Anime)
- Integration into library
- Analysis and commit of results to AudioMirror repo

Result: fully automated, no manual Mp3tag or separate analysis step needed.

## Stage 4: Sync to Device

*(manual - cannot automate)*

1. Open iTunes and ensure device is detected
2. Add Audio folder to iTunes
3. File → Library → Show Duplicate Items → remove duplicates
4. Check for broken files (exclamation symbol on far left)
5. Sync device twice to pick up new music

---

## Reference

See **Music-Library-Rules.md** for:
- Library folder structure
- Tag format and rules
- Track routing logic
- Folder-specific rules (Musivation, Sources, etc.)
- Known gotchas

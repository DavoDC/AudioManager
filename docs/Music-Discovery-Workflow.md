# Music Discovery to Device Workflow

Complete end-to-end process: Spotify discovery → local library → phone sync.

## Stage 1: Discovery

Discover on Spotify via release radar - add to liked songs.

For each new song, check that artist out - look at top 10 streamed songs, find other tracks you like.

## Stage 2: Acquiring

1. Add all songs to a playlist and remove all songs from liked songs
2. Run `C:\Users\David\GitHubRepos\SpotifyPlaylistGen\scripts\open_playlist_in_manager` on that playlist
3. This script opens each track in a special service in browser, then places songs in `C:\Users\David\Downloads\NewMusic\`

> **Note:** SpotifyPlaylistGen is currently rough. Future: integrate into SpotifyPlaylist tool for seamless workflow.

## Stage 3: Integrate

1. Apply rules using Mp3tag in `Downloads/NewMusic` folder (tag cleanup, filename format)
   - Note: TCMP and Akira The Don genre are set automatically by AudioManager - no need to set in Mp3tag
2. Run AudioManager integrate (dry run first, then real) - routes files into library per Music-Library-Rules
3. Run AudioManager analysis - checks library integrity, commits audio report and AudioMirror changes

> **Future:** AudioManager should handle all tag rules automatically (TCMP, genre assignment for Musivation) so Mp3tag step becomes optional.

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

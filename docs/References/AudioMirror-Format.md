# AudioMirror Format

## Repo
- Path: `C:\Users\David\GitHubRepos\AudioMirror`
- Commit format: `Mar 8 Update` (abbreviated month + day number + "Update")

## Commit Rules
- **Do NOT commit if LibChecker reports any issues.** Fix all issues first, then commit both AudioMirror and the audio report together.
- If LibChecker is clean, the audio report shows a single `LibChecker: Clean` line.
- If LibChecker has issues, they are prominently flagged in the report and the AudioMirror commit is blocked until resolved.

## XML Format

Each track is an individual `.xml` file. Current structure (as of 2026-06-03):
```xml
<Track>
  <Title>Song Name</Title>
  <Artists>Artist1;Artist2</Artists>   <!-- semicolon-separated, first = primary -->
  <Album>Album Name</Album>
  <Year>2001</Year>
  <TrackNumber>1</TrackNumber>
  <Genres>Rap; Hip Hop</Genres>
  <Length>00:04:38.9520000</Length>
  <AlbumCoverCount>1</AlbumCoverCount>
  <AlbumCover>
    <Count>1</Count>
    <Width>800</Width>
    <Height>800</Height>
  </AlbumCover>
  <Compilation>True</Compilation>
</Track>
```

**Planned schema redesign (Phase 2.5 - see IDEAS.md):** The current `AlbumCoverCount` + `AlbumCover/Count/Width/Height` structure is broken for tracks with 2+ covers (only one Width/Height pair is stored). Target schema replaces it with a zero-to-N collection:
```xml
<!-- typical: one cover -->
<CoverArt><Cover width="800" height="800" /></CoverArt>
<!-- two covers -->
<CoverArt>
  <Cover width="800" height="800" />
  <Cover width="500" height="500" />
</CoverArt>
<!-- no cover -->
<CoverArt />
```
Update this doc once the redesign is implemented (force regen required).

## Folder Structure

Under `AUDIO_MIRROR/`:
- `Artists/<ArtistName>/<Album>/` - standard tracks
- `Compilations/`, `Miscellaneous Songs/`, `Motivation/`, `Musivation/`, `Sources/` - special categories

`audiomirror_path` in SpotifyPlaylistGen config should point to the `AUDIO_MIRROR/` folder (not a single file).

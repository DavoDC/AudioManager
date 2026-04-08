# AudioMirror Format

## Repo
- Path: `C:\Users\David\GitHubRepos\AudioMirror`
- Commit format: `Mar 8 Update` (abbreviated month + day number + "Update")

## Commit Rules
- **Do NOT commit if LibChecker reports any issues.** Fix all issues first, then commit both AudioMirror and the audio report together.
- If LibChecker is clean, the audio report shows a single `LibChecker: Clean` line.
- If LibChecker has issues, they are prominently flagged in the report and the AudioMirror commit is blocked until resolved.

## XML Format

Each track is an individual `.xml` file. Structure:
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
  <Compilation>True</Compilation>
</Track>
```

## Folder Structure

Under `AUDIO_MIRROR/`:
- `Artists/<ArtistName>/<Album>/` - standard tracks
- `Compilations/`, `Miscellaneous Songs/`, `Motivation/`, `Musivation/`, `Sources/` - special categories

`audiomirror_path` in SpotifyPlaylistGen config should point to the `AUDIO_MIRROR/` folder (not a single file).

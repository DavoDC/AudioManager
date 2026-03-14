# DWave — Ideas & Future Vision

## Dashboard Concept

AudioManager is doing too much — break it into modular components. Build a tabbed dashboard:

- **Analytics tab** — graphs: library stats, genre distribution, listening trends
- **Mirror tab** — current AudioMirror functionality (sync tracking, diff view)
- **Tagging tab** — tag editing, batch fixes, AudioManager analysis workflow
- **Library browser tab** — browse artists/albums/tracks
- **Cross-synthesis tab** — pull together Last.fm scrobbles, Spotify data, and offline library

This replaces the monolithic AudioManager with a data-rich, tabbed music management hub.

**Related:** SpotifyPlaylistGen (Priority 1 in Directives.md) feeds into this — once Spotify integration exists as a standalone tool, it can be embedded as a tab here.

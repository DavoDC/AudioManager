# Audio Manager

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/G2G31WKOCN)



A C# console application for managing, analysing, and auditing a personal music library - generates metadata statistics, validates organisation rules, and automatically integrates new music.

## What it does

- **Mirror** - creates a lightweight XML snapshot of the library metadata (see [AudioMirror](https://github.com/DavoDC/AudioMirror))
- **Analyse** - generates timestamped statistical reports, runs full library validation (LibChecker), and auto-commits AudioMirror if the library is clean
- **Integrate** - scans a staging folder for new MP3s, pre-processes tags, routes files automatically, and produces a confidence report

## Usage

Run via the launcher or CLI:

- **Launcher:** `scripts/launch.bat` - builds via MSBuild, then presents a menu (analysis, analysis with force regen, integration dry run, integration real)
- **CLI:** `AudioManager.exe analysis [--force-regen]` or `AudioManager.exe integrate [--dry-run]`
- **Dry run:** always run integration with `--dry-run` first - prints every planned action without moving any files

## Statistics Provided

- Total playback hours, library size on disk, average file size
- Average and median song length and track age
- Percentage and count breakdown of: artists, genres, release years, decades

For listening statistics, visit [LastFM](https://www.last.fm/user/david369music).

## Integration Pipeline

**One command handles complete integration:** drop new MP3s in `Downloads/NewMusic`, run integrator mode, program handles everything:

1. **Tag pre-processing** - adds `TCMP=1` to all tracks, sets `Genre=Musivation` for Akira The Don, removes unwanted tag strings per rules
2. **Filename cleanup** - renames files per naming convention (artist - title.mp3)
3. **Scan-ahead** - identifies artists hitting the 3-song threshold (routes to `Artists/` instead of `Misc`)
4. **Auto-routing** - routes to destination folders: Artists, Musivation, Motivation, Compilations, Misc, or Sources (with optional user prompt for Films/Shows/Anime)
5. **Library integration** - moves files into the library
6. **Analysis and commit** - runs full library validation (LibChecker), generates analysis report, auto-commits results to AudioMirror repo

No manual Mp3tag editing needed. Dry-run mode (`--dry-run`) shows all planned changes without moving files.

## Project Structure

```
AudioManager/
├── config/        # LibChecker exceptions config (libchecker-exceptions.xml)
├── docs/          # IDEAS.md, HISTORY.md, design and planning docs
├── logs/          # Integration run logs - gitignored
├── project/       # C# solution and source code
├── reports/       # Auto-generated timestamped analysis reports - gitignored
└── scripts/       # Launchers (launch.bat) and utility scripts
```

## Tech

- **Language:** C# (.NET Framework 4.8)
- **Metadata:** [TagLib#](https://github.com/mono/taglib-sharp) for ID3 tag reading and writing
- **Architecture:** `Doer` base class - every operation is timed automatically
- **Output:** `TeeWriter` captures console output to both screen and file simultaneously

## Development

**Developed:** November 2023 · **Status:** Actively maintained

## Dependencies

- [TagLib#](https://github.com/mono/taglib-sharp)

## Related

- **[AudioMirror](https://github.com/DavoDC/AudioMirror)** - XML mirror of the music library, auto-generated and auto-committed by this tool

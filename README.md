# Audio Manager

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/G2G31WKOCN)



A C# console application for managing, analysing, and auditing a personal music library - generates metadata statistics, validates organisation rules, and automatically integrates new music.

## What it does

- **Mirror** - creates a lightweight XML snapshot of the library metadata (see [AudioMirror](https://github.com/DavoDC/AudioMirror))
- **Analyse** - generates timestamped statistical reports and runs full library validation (LibChecker)
- **Integrate** - scans a staging folder for new MP3s, pre-processes tags, routes files automatically, and produces a confidence report

## Statistics Provided

- Total track count, total playback hours, library size on disk
- Average and median song length and track age
- Percentage and count breakdown of: artists, genres, release years, decades

For listening statistics, visit [LastFM](https://www.last.fm/user/david369music).

## Integration Pipeline

New music integration is fully automated for standard cases:

1. **Scan-ahead** - pre-scans the batch to identify artists hitting the 3-song threshold (routes to `Artists/` instead of `Misc`)
2. **Tag pre-processing** - sets `TCMP=1` on all incoming tracks; sets `Genre=Musivation` for Akira The Don tracks
3. **Auto-routing** - standard routes (Musivation, Motivation, existing Artists folder) are accepted automatically; only ambiguous Misc routing prompts the user
4. **Confidence report** - count check, per-file table, destination sanity check, error summary
5. **Integration log** - saved to `logs/integration-YYYYMMDD.txt`
6. **AudioMirror auto-commit** - commits and pushes AudioMirror after every clean LibChecker run

## Project Structure

```
AudioManager/
├── config/        # LibChecker exceptions config (libchecker-exceptions.xml)
├── docs/          # IDEAS.md, HISTORY.md, design and planning docs
├── logs/          # Integration run logs (gitignored)
├── project/       # C# solution and source code
├── reports/       # Auto-generated timestamped analysis reports (gitignored)
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

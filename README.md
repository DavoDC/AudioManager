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
- **CLI:** `AudioManager.exe analysis [--force-regen]` | `AudioManager.exe tagfix [--dry-run]` | `AudioManager.exe integrate [--dry-run]`
- **Dry run:** always run with `--dry-run` first - prints every planned action without modifying files

## LibChecker Validations

Ensures the library conforms to organization rules:
- **Filename matching** - all artists from tag appear in filename
- **Tag completeness** - Title, Artists, Album, Year present on all tracks
- **Unwanted strings** - flags feat., ft., edit, bonus, original, soundtrack, version, explicit in tags
- **Album cover** - exactly 1 cover per track (APIC frame)
- **Compilation flag** - TCMP=1 on all tracks
- **Duplicates** - no artist+title duplicates (detects re-integrated songs)
- **Artist folder structure** - Artists with 3+ songs have dedicated folders
- **Album subfolders** - 2+ songs from same album → album subfolder; 1 song → Singles/
- **Misc folder review** - warns if Misc folder has grown too large
- **Genre consistency** - Musivation/Motivation folders have matching genre tag
- **Sources validation** - soundtrack tracks properly named and organized

Run: `AudioManager.exe analysis` (includes LibChecker in the pipeline)

## Statistics Provided

- Total playback hours, library size on disk, average file size
- Average and median song length and track age
- Percentage and count breakdown of: artists, genres, release years, decades

For listening statistics, visit [LastFM](https://www.last.fm/user/david369music).

## Integration Pipeline

**Automated end-to-end workflow:** drop new MP3s in `Downloads/NewMusic`, run the three-step sequence below:

### Step 1: Tag Fixing (TagFixer)
Cleans raw MP3s before integration:
- Removes parenthetical phrases from titles (e.g., `(feat. Artist)`, `(Remix)`)
- Extracts featured artists and adds to the artist tag
- Renames files per convention: `{artists} - {title}.mp3`
- Sets `TCMP=1` on all tracks
- Sets genre for special artists (Akira The Don → Musivation)

Run: `AudioManager.exe tagfix [--dry-run]`

### Step 2: Integration (MusicIntegrator)
Routes cleaned files into the library:
- Scan-ahead identifies artists hitting the 3-song threshold (routes to `Artists/` instead of `Misc`)
- Auto-routes to destination folders based on metadata: Artists (with album subfolders), Musivation, Motivation, Misc
- Optional folder picker for ambiguous routes (Films/Shows/Anime media, Compilations)

Run: `AudioManager.exe integrate [--dry-run]`

### Step 3: Analysis & Validation (Analyser + LibChecker)
- Regenerates AudioMirror XML with newly integrated tracks
- Runs full library validation (LibChecker), generates timestamped report
- Auto-commits AudioMirror if library is clean

Auto-runs after step 2, or manually: `AudioManager.exe analysis`

**No manual MP3tag editing needed.** Always use `--dry-run` first to preview changes.

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

# AudioManager GUI

A dark, Sonarr-style dashboard that replaces the CLI and BAT scripts for day-to-day
use of AudioManager: live library statistics, a visual staged integration workflow
with per-track album art and routing preview, and a browsable album-art wall of the
whole library - all without opening a terminal.

## Launch

Double-click **`scripts\launch-gui.bat`** - starts windowless and opens the app in
your browser at `http://127.0.0.1:8471`.

**Desktop / Start-Menu shortcut:** right-click `scripts\launch-gui.bat` >
*Send to* > *Desktop (create shortcut)* (or copy a shortcut into
`shell:Start Menu Programs`). Optionally set the icon via shortcut Properties.

First-time setup: `pip install -r gui/requirements.txt`

Dev run with console output: `python -m gui.main` from the repo root.
Deep-link a tab: `http://127.0.0.1:8471/?tab=integration`

## Architecture (the one rule that matters)

**The GUI parses no AudioMirror XML - ever.** C# owns 100% of data ingestion:

```
AudioManager.exe analysis --json-output     (triggered from the Statistics header)
        |
        +--> logs/analysis-stats.json   -> Statistics tab   (aggregate stats)
        +--> logs/tracks.json           -> Library Browser  (per-track array)

AudioManager.exe integrate --dry-run --json-output   (Integration tab Scan)
        |
        +--> logs/routing-<timestamp>.json -> review queue
```

`gui/data_loader.py` is the single consumer of the two contract files
(schema: `docs/References/AnalysisJson-Format.md`; it checks `schemaVersion`
and fails loudly on a mismatch). `gui/runner.py` serializes all exe calls
(one at a time, always `--no-input`, timeout + cancel, stdout+stderr streamed
line by line). Album art is read from MP3s directly via mutagen (an image
read, not XML parsing) and cached in `gui/.cache/thumbs/`.

**Write safety:** GUI/Python code never writes to the music library, the
NewMusic inbox, or AudioMirror. All file operations happen inside the exe's
own modes, behind its own safety gates. The GUI's only writes are its cache
(`gui/.cache/`).

## Tabs

| Tab | State | Notes |
|---|---|---|
| Statistics | Full | All panels + chart-type swaps + date window + analysis freshness controls |
| Integration | Full (accept-all) | Staged scan/review/confirm/execute; declines block execution (see gaps) |
| Library Browser | MVP | Table/grid, real album art, search, filter chips, column picker, pagination |
| Tag Fix | Skeleton | Documents the exe's fixed transforms; Run = `tagfix --dry-run` |
| Mirror | Functional | Status + one-click Commit AudioMirror (confirm dialog, local commit only) |
| Services | Placeholder | Last.fm / Spotify stretch stubs (far future) |

Integration is fully selective: declined tracks are excluded via
`integrate --manifest gui/.cache/accepted-manifest.json` and stay untouched
in NewMusic. **Remaining gap (surfaced in the UI):** tag rules are not
configurable without a C# change. Details: `docs/References/GUI-Architecture.md`.

## Tests

```
python -m pytest gui/tests -q
```

Unit tests cover `data_loader` field mapping, filtering/pagination,
empty-library states, and the loud `schemaVersion` failure. A contract smoke
test asserts every field the GUI reads exists in the real `logs/*.json`
(skips if analysis has never run).

### Manual smoke checklist (subprocess flows - can't be unit-tested safely)

1. `python gui/tests/manual_check_art.py` - real album-art extraction PASSes.
2. Statistics > **Re-run analysis** - inline progress, dashboard refreshes, "last run" updates.
3. Statistics > **Force full regen** - confirm dialog, long run survives, **Cancel** kills it.
4. Integration > **Scan NewMusic** - empty inbox shows the empty state; with files, review cards
   render art/destination/reason/tag chips.
5. Force a failure (e.g. scan with a dirty AudioMirror) - the error modal shows the parsed
   `- ERROR:` cause; **Copy** puts command + output on the clipboard; **Retry** re-runs it.
6. While any run is active, trigger buttons elsewhere are disabled (one exe call at a time).
7. Library Browser grid page 1 renders real covers; paging extracts only the new page.

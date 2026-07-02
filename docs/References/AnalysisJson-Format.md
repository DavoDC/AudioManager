# Analysis JSON Format (`analysis --json-output`)

The GUI's data contract. `AudioManager.exe analysis --json-output` writes **two** files to `logs/`
(fixed filenames, latest run wins):

| File | Source | Contains | GUI consumer |
|---|---|---|---|
| `analysis-stats.json` | `Code/Doer/Analyser/StatsJson.cs` | aggregate library statistics | Statistics tab |
| `tracks.json` | `Code/Doer/Analyser/TracksJson.cs` | the full per-track array | Library Browser tab |

**Why both exist / the hard rule they enforce:** the Python GUI must stay **100% decoupled from the raw
AudioMirror XML**. C# owns all data ingestion - it is the only XML parser. `StatsJson` reuses the exact
`StatList` primitives the text report uses (so stats can't drift from `AudioReport.md`), and `TracksJson`
emits every per-track row the Library Browser needs (so Python never globs or parses XML to list tracks).
**Do NOT reimplement distributions in Python. Do NOT parse `AudioReport.md`. Do NOT parse AudioMirror XML
in Python.** Consume these two files.

---

## `analysis-stats.json` (aggregate statistics)

**Versioning:** `schemaVersion` starts at 1. It is bumped only when a field is removed or its meaning
changes (additive fields do not bump it). The GUI should read `schemaVersion` and refuse a mismatch it
was not built against, rather than silently mis-reading.

## Shape

```jsonc
{
  "schemaVersion": 1,
  "generatedAt": "2026-07-03T01:00:56",     // ISO-8601 local time, no offset

  "summary": {
    "trackCount": 5694,
    "artistCount": 1944,                     // distinct artists (multi-artist tracks split on ; and ,)
    "genreCount": 53,                        // distinct genres (same splitting)
    "totalLibraryBytes": 30558669644,        // real on-disk sum of *.mp3 (0 if the Audio folder is unreadable)
    "avgFileBytes": 5366819,                 // totalLibraryBytes / trackCount
    "totalPlaybackSeconds": 1299353.903,
    "totalPlaybackHours": 360.9,             // rounded to 1 dp
    "avgSongLengthSeconds": 228,
    "medianSongLengthSeconds": 211
  },

  // Each distribution is an array of {label, count}, sorted DESCENDING by count.
  // A track with multiple genres/artists is counted once per genre/artist (matches the report).
  "genreDistribution":  [ {"label": "Hip Hop", "count": 2669}, ... ],   // all distinct genres
  "decadeDistribution": [ {"label": "2020s",   "count": 2188}, ... ],   // all decades
  "yearDistribution":   [ {"label": "2022",    "count": 515},  ... ],   // all years present

  "topArtists": {
    "exclMusivation": [ {"label": "Eminem",        "count": 155}, ... ],  // top 50, Musivation-genre tracks removed
    "all":            [ {"label": "Akira The Don", "count": 510}, ... ]   // top 50, everything
  },

  "ageStats": {                              // years; null if no track has a parseable Year
    "averageYears": 11.6,
    "medianYears": 9,
    "newestYears": 0,                        // age of the newest track (currentYear - maxYear)
    "oldestYears": 92
  },
  "ageDistribution": [                        // fixed 5 buckets, always present in this order
    {"label": "0-2y",   "count": 580},        // age <= 2
    {"label": "2-5y",   "count": 1194},       // 2 < age <= 5
    {"label": "5-10y",  "count": 1364},       // 5 < age <= 10
    {"label": "10-20y", "count": 1777},       // 10 < age <= 20
    {"label": "20y+",   "count": 779}         // age > 20
  ],

  "coverArt": {
    "total": 5694,
    "hasKnownDims": 5694,                     // cover present and dimensions parsed
    "noCover": 0,                            // CoverWidth == "0"
    "missingData": 0,                        // CoverWidth empty (pre-force-regen XMLs)
    "unknownFormat": 0,                      // CoverWidth == "Unknown" (art present, format unreadable)
    "subMin800": 247,                        // known dims with min(w,h) < 800
    "nonSquare": 97,                         // known dims with w != h
    "dimensionHistogram": [ {"label": "800x800", "count": 3080}, ... ]  // top 15 "WxH" buckets, desc
  },

  "tagCompleteness": {                        // % of tracks with Title+Artist+Album+Genre+Year all present
    "complete": 5334,                        // "present" = non-blank and not the literal "Missing"
    "total": 5694,
    "percent": 93.7
  },
  "coverCoverage800": {                        // % of ALL tracks whose cover is >= 800px on the short side
    "covered": 5447,                         // known dims with min(w,h) >= 800
    "total": 5694,
    "percent": 95.7
  }
}
```

## Consumption notes for the GUI

- **Read this file; do not shell the exe per panel.** The GUI runs `analysis --json-output` once at
  startup / on manual refresh, then reads `logs/analysis-stats.json`. All Statistics panels and stat
  tiles map 1:1 to a field above.
- **Per-track rows** (Library Browser table/grid) are NOT in this file - it is aggregate stats only.
  Per-track data comes from globbing the AudioMirror XML directly (see `AudioMirror-Format.md`).
- **`totalLibraryBytes` is real**, computed from a single disk walk inside the exe. This removes the
  earlier plan to fake file size from bitrate in Python - the accurate number is now free.
- **Numbers are culture-invariant** (always `.` decimal), safe to `JSON.parse` / `json.loads` regardless
  of the machine locale. There is a regression test guarding this (`Build_UsesInvariantDecimalUnderCommaCulture`).
- **Missing/degenerate data:** on an empty library, distributions are `[]`, `ageStats` fields are `null`,
  and percents are `0`. The GUI should render empty states, not crash.

---

## `tracks.json` (per-track array for the Library Browser)

One object per track, every track. ~4 MB for 5,694 tracks - loaded once at startup (and on refresh),
sliced/filtered in memory by `data_loader`. This file is why Python never needs to touch the XML.

```jsonc
{
  "schemaVersion": 1,
  "generatedAt": "2026-07-03T01:12:16",
  "trackCount": 5694,
  "tracks": [
    {
      "id": "\\Sources\\YouTube\\PlentaKill\\PlentaKill - Don't You Worry Bot.xml",  // mirror-relative path, unique + stable
      "title": "Don't You Worry Bot",
      "artists": "PlentaKill",              // full field, ; or , separated if multiple
      "primaryArtist": "PlentaKill",        // first artist
      "album": "...",
      "year": "2010",                       // string; "Missing" if absent
      "decade": "2010s",                    // derived; null if year unparseable
      "genres": "Games",                    // full field, "; " separated
      "primaryGenre": "Games",              // first genre
      "trackNumber": "1",
      "lengthSeconds": 213.5,               // null if unparseable
      "length": "3:33",                     // m:ss, "" if unknown
      "compilation": false,                 // boolean (from the "True"/"False" XML string)
      "coverWidth": 1200,                   // int, or null if no readable art
      "coverHeight": 1200,
      "hasArt": true,                       // cover present with parseable dimensions
      "hiResArt": true,                     // hasArt AND min(w,h) >= 800
      "addedDate": "2026-07-01",            // mirror-XML mtime (ISO date), or null
      "filePath": "C:\\Users\\David\\Audio\\Sources\\YouTube\\PlentaKill\\PlentaKill - Don't You Worry Bot.mp3"
    }
    // ... one per track
  ]
}
```

### Consumption notes

- **`filePath` is the real MP3 path**, reconstructed from the mirror path (the AudioMirror mirrors the
  Audio folder 1:1). Use it with `mutagen` to extract embedded album art for the grid view. Extract
  lazily (current page only) and cache thumbnails - do not read 5,694 covers at startup.
- **`filePath` is best-effort:** for the rare track whose filename was sanitised into a different XML
  name, the reconstructed path won't exist on disk. Treat a missing file as "no extractable art" and
  show a placeholder - never crash. (Verified 2026-07-03: a random sample of 20 + the first track all
  resolved to real MP3s; the exception is genuinely rare.)
- **`hasArt`/`hiResArt`** are the album-art *status* (Python doesn't need to open the file to know it) -
  use them to decide whether to attempt extraction and to badge low-res covers.
- **`id`** is the stable per-track key for selection/paging state; it is the mirror-relative `.xml` path
  (the source of truth), NOT the `.mp3` path.
- Numbers are culture-invariant, same as the stats file.

## Versioning both files

`StatsJson.SchemaVersion` and `TracksJson.SchemaVersion` are independent. The GUI's `data_loader` should
check each file's `schemaVersion` against what it was built for and fail loudly on a mismatch rather than
silently mis-reading. Additive fields do not bump the version; removing a field or changing its meaning does.

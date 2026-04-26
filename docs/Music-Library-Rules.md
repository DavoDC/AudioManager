# Music Library Rules

**Reference guide.** For the complete end-to-end workflow (Spotify discovery → tagging → device sync), see **Music-Discovery-Workflow.md**.

## Paths
- Library: `C:\Users\David\Audio\`
- New music inbox: `C:\Users\David\Downloads\NewMusic\`
- AudioManager tool: `C:\Users\David\GitHubRepos\AudioManager\` (compiled exe in `bin/Release/`)

## Library Structure
```
Audio/
  Artists/{artist}/{album or Singles}/
  Compilations/
  Miscellaneous Songs/
  Motivation/
  Musivation/{artist}/{album or Singles}/
  Sources/
    Films/{film name}/
    Shows/{show name}/
    Anime/{anime name}/
```

---

## Global Tag Rules (all tracks)
- All files must be `.mp3` format
  - To convert many: Format Factory -> To MP3 -> Add files -> Output to source folder
  - Search Explorer for `NOT *.mp3 AND NOT kind:folder` to find non-MP3s
- `TCMP` = 1 (Compilation) on every track - stops iTunes grouping each track as a separate album
- Required tags: Title (TIT2), Artists (TPE1), Album (TALB), Year (TDRC), exactly 1 album cover (APIC)
- If no album exists, use the track Title as the Album value
- Featured artists semicolon-separated in TPE1, primary artist first: `MainArtist;FeatArtist`
- Remove from ALL tag fields: `feat.`, `ft.`, `Edit`, `Version`, `Original`, `Soundtrack`, `Explicit`
  - "feat." info belongs in TPE1 only, not in TIT2 or TALB
- Genre must match folder for Musivation/Motivation tracks

## Filename Convention
- Format: `{all TPE1 artists semicolon-separated} - {title}.mp3`
- Example: `Chiddy Bang;Icona Pop - Mind Your Manners.mp3`
- Use MP3Tag's tag-to-filename feature: `%artist% - %title%`

---

## Routing Rules (priority order)
1. Genre = `Musivation` -> `Musivation/` (see Akira The Don / Loot Bryon Smith rules)
2. Genre = `Motivation` -> `Motivation/`
3. Compilation album -> `Compilations/`
4. Discovered via film/show/anime -> `Sources/` (see Sources rules below)
5. Artist folder exists in `Artists/` -> `Artists/{primaryArtist}/` + album subfolder if applicable
6. 3+ songs same artist accumulate in Misc -> move all to new `Artists/{artist}/` folder
7. Otherwise -> `Miscellaneous Songs/`

## Album Subfolder Rule
- 2+ songs from same album -> create/use album subfolder
- Only 1 song from an album -> put in `Singles/`, don't create album folder
- No loose files directly in an artist folder

---

## Folder: Artists
- Artists who have 3 or more songs in the library
- Albums go in named subfolders; remaining singles go in `Singles/`
- No loose files directly in the artist folder

## Folder: Compilations
- Full compilation albums (various artists, released as a compilation)

## Folder: Miscellaneous Songs
- Random singles that don't meet the 3-song artist threshold
- Periodically review: look for trios to promote to `Artists/`, look for soundtracks to move to `Sources/`

## Folder: Musivation
- All songs must have genre `Musivation`
- See Akira The Don and Loot Bryon Smith rules below

## Folder: Motivation
- All songs must have genre `Motivation`

## Folder: Sources
Tracks discovered through a specific form of media.

**Films** (`Sources/Films/{film name}/`)
- Album field must contain `OST` (e.g. `Kabhi Khushi Kabhie Gham OST`)
- Strip `Original Motion Picture Soundtrack` -> replace with `OST`
- Strip `original` and `soundtrack` from all other fields

**Shows** (`Sources/Shows/{show name}/`)
- Album field should be `{Show Name} OST`
- Strip `feat.` and unwanted strings from tags per global rules

**Anime** (`Sources/Anime/{anime name}/`)
- Use English titles for memorability
- Use anime name as Album
- Full-length version for songs you like, anime-length (OP/ED cut) for lesser ones
- Replace cover art with anime cover art

---

## Akira The Don (Musivation)
- All ATD tracks: genre = `Musivation`, destination = `Musivation/Akira The Don/`
- TPE1 = `Akira The Don;{Sampled Person}` (sampled person MUST be listed as co-artist)
- Routing within ATD folder:
  - `People/{person}/` folder exists -> route there (may have album subfolders inside)
  - No People subfolder -> `Singles/`
- Example: "NO SMARTER THAN YOU" (Steve Jobs) -> no `People/Steve Jobs/` -> `Singles/`

## Loot Bryon Smith
- Lives in `Musivation/Loot Bryon Smith/` (NOT Artists)
- Genre must be `Musivation`
- Subfolder structure: `Spotify Albums/{album}/`, `Spotify Singles/`, `YouTube Singles/`

---

## Known Gotchas
- Jay-Z: folder is `Jay-Z` - TPE1 and filename must match exactly (not `JAY-Z`)
- `mike.` artist: folder name is `mike` (no trailing dot - Windows limitation)
- Album names containing `Edition` (e.g. `One Of The Boys (15th Anniversary Edition)`) are fine to keep - LibChecker has an exception for this

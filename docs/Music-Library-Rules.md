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

**AUTOMATED VIA TAGFIXER** - These rules are automatically applied by the TagFixer module before integration. Do not manually fix these; the program handles them.

- All files must be `.mp3` format
  - To convert many: Format Factory -> To MP3 -> Add files -> Output to source folder
  - Search Explorer for `NOT *.mp3 AND NOT kind:folder` to find non-MP3s
- `TCMP` = 1 (Compilation) on every track - stops iTunes grouping each track as a separate album
  - **AutoFixed:** TagFixer sets TCMP=1 on all files from NewMusic
- Required tags: Title (TIT2), Artists (TPE1), Album (TALB), Year (TDRC), exactly 1 album cover (APIC)
  - If no album exists, use the track Title as the Album value
- Featured artists semicolon-separated in TPE1, primary artist first: `MainArtist;FeatArtist`
  - **AutoFixed:** TagFixer ensures all featured artists are in TPE1, primary first
- Remove from ALL tag fields: `feat.`, `ft.`, `Edit`, `Version`, `Original`, `Soundtrack`, `Explicit`, `(Album Version)`, `(Radio Edit)`
  - "feat." info belongs in TPE1 only, not in TIT2 or TALB
  - **AutoFixed:** TagFixer strips these from Title and Album tags
- Genre must match folder for Musivation/Motivation tracks
  - **AutoFixed:** TagFixer sets genre for Musivation/Motivation tracks if not already set

## Filename Convention

**AUTOMATED VIA TAGFIXER** - Filenames are automatically set by the TagFixer module before integration based on the TPE1 (artist) and TIT2 (title) tags.

- Format: `{all TPE1 artists semicolon-separated} - {title}.mp3`
- Example: `Chiddy Bang;Icona Pop - Mind Your Manners.mp3`
  - Not: `Chiddy Bang - Mind Your Manners (feat. Icona Pop).mp3`
  - The "(feat. Icona Pop)" is removed from title tag and Icona Pop is added to TPE1 artist tag
- **AutoFixed:** TagFixer renames files to match this convention, pulling from cleaned TPE1 and TIT2 tags
- Manual alternative: Use MP3Tag's tag-to-filename feature: `%artist% - %title%` (but TagFixer should handle this automatically)

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
Tracks discovered through a specific form of media. **CRITICAL DISTINCTION:**

**Official Soundtracks** (album IS the soundtrack compilation, e.g., "Peacemaker (Original Soundtrack)")
- Album field format: `{Show/Movie Name} OST` (e.g., `Peacemaker OST`, `The White Lotus OST`)
- Strip `Original Motion Picture Soundtrack` -> replace with show/movie name + OST
- Folder: `Sources/Shows/{show name}/` or `Sources/Films/{film name}/`
- Example: Cristobal Tapia de Veer - Aloha! -> Album: "The White Lotus OST"

**Songs featured in shows/movies** (regular artist album that appears in credits, NOT a soundtrack)
- **DO NOT add OST to album tag** - album is the artist's original album name
- Album field: keep original (e.g., `Hunting High and Low`, `The Very Best of Bonnie Tyler`)
- Folder: either leave in `Artists/` OR move to `Sources/Shows/{show name}/` with original album tag intact
- Example: a-ha - Take on Me -> Album: "Hunting High and Low" (NOT "OST"), Folder: Sources/Shows/The Super Mario Bros. Movie/
- **Why:** Adding OST is misleading - this is NOT an official soundtrack album, just a featured track
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

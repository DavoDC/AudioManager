# Music Library Rules

## Paths
- Library: `C:\Users\David\Audio\`
- New music inbox: `C:\Users\David\Downloads\NewMusic\`
- AudioManager tool: `C:\Users\David\GitHubRepos\AudioManager\` (compiled exe in `bin/Release/`)

## Library Structure
```
Audio/
  Artists/{artist}/{album}/
  Miscellaneous Songs/
  Musivation/{artist}/{album or Singles}/
  Motivation/
  Compilations/
```

## Tag Rules (all tracks)
- `TCMP` = 1 (Compilation) on every track
- Required tags: Title (TIT2), Artists (TPE1), Album (TALB), Year (TDRC), exactly 1 album cover (APIC)
- Featured artists semicolon-separated in TPE1, primary artist first: `MainArtist;FeatArtist`
- "feat." must NOT appear in TIT2 or TALB - strip it. Featured info belongs in TPE1 only.
- Genre must match folder for Musivation/Motivation tracks

## Filename Convention
- Format: `{all TPE1 artists semicolon-separated} - {title}.mp3`
- Example: `Chiddy Bang;Icona Pop - Mind Your Manners.mp3`

## Routing Rules (priority order)
1. Genre = `Musivation` -> `Musivation/` (see Akira The Don / Loot Bryon Smith rules)
2. Genre = `Motivation` -> `Motivation/`
3. Artist folder exists in `Artists/` -> `Artists/{primaryArtist}/` + album subfolder if applicable
4. 3+ songs same artist accumulate in Misc -> move all to new `Artists/{artist}/` folder
5. Otherwise -> `Miscellaneous Songs/`

## Album Subfolder Rule
- 2+ songs from same album -> create/use album subfolder
- Only 1 song from an album -> put in `Singles/`, don't create album folder

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

## Known Gotchas
- Jay-Z: folder is `Jay-Z` - TPE1 and filename must match exactly (not `JAY-Z`)
- `mike.` artist: folder name is `mike` (no trailing dot - Windows limitation)

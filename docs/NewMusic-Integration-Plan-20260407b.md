# New Music Integration Plan - 2026-04-07 (Batch B)

> **Status: COMPLETE** - Integration done. See issues below.

## Execution Steps

1. Create `C:\Users\David\Audio\Artists\Red Hot Chili Peppers\Californication\`
2. Move 3 RHCP files there
3. Move `mike. - north star.mp3` to `C:\Users\David\Audio\Artists\mike\Singles\`
4. Create `C:\Users\David\Audio\Sources\Films\Kabhi Khushi Kabhie Gham\`
5. Move 2 Bollywood files there
6. Move remaining 14 files to `C:\Users\David\Audio\Miscellaneous Songs\`
7. Verify `Downloads\NewMusic\` is now empty
8. Run AudioManager in Visual Studio (Analysis mode) - lib checker will flag any issues

---

## LibChecker Output

```
Checking library...
 - Checking all tags against filenames..
 - Checking all tags for unwanted/missing info...
  - Found 'feat.' in album of 'Victorious Cast;Victoria Justice - Make It Shine (Victorious Theme)'
  - Found 'original' in album of 'Jatin-Lalit;Amit Kumar;Sonu Nigam;Alka Yagnik;Udit Narayan;Kavita Krishnamurthy - Bole Chudiyan'
  - Found 'soundtrack' in album of 'Jatin-Lalit;Amit Kumar;Sonu Nigam;Alka Yagnik;Udit Narayan;Kavita Krishnamurthy - Bole Chudiyan'
  - Found 'original' in album of 'Jatin-Lalit;Lata Mangeshkar - Kabhi Khushi Kabhie Gham'
  - Found 'soundtrack' in album of 'Jatin-Lalit;Lata Mangeshkar - Kabhi Khushi Kabhie Gham'
  - Total hits: 5
 - Checking all tags for duplicates...
 - Checking Artists folder...
 - Checking Miscellaneous Songs folder...
  - There are 3 songs by 'The Goo Goo Dolls' in the Miscellaneous Songs folder!
  - Total hits: 1
 - Checking Musivation folder...
 - Checking Motivation folder...
```

## Issues to Fix (via mp3tag + file move)

| # | Track | Issue | Fix |
|---|---|---|---|
| 1 | `Victorious Cast;Victoria Justice - Make It Shine (Victorious Theme)` | `feat.` in album tag | Remove "feat." from album tag |
| 2 | `Jatin-Lalit;...;Kavita Krishnamurthy - Bole Chudiyan` | `original` + `soundtrack` in album tag | Remove those words from album tag |
| 3 | `Jatin-Lalit;Lata Mangeshkar - Kabhi Khushi Kabhie Gham` | `original` + `soundtrack` in album tag | Remove those words from album tag |
| 4 | `The Goo Goo Dolls` | 3 songs in Misc - threshold met | FIXED - Iris + Slide to `Artists/The Goo Goo Dolls/Dizzy up the Girl/`, 3rd track to `Singles/` |

NOTES FOR CLAUDE TO PROCESS: 
updated 2 bollywood songs to use "OST" in album as per other tracks in tht films folder
C:\Users\David\Audio\Sources\Films

Victorious , realised in belonged in Shows folder
ALBUM FIELD: Victorious: Music From The Hit TV Show (feat. Victoria Justice) -> Victorious OST
"C:\Users\David\Audio\Sources\Shows\Victorious\Victorious Cast;Victoria Justice - Make It Shine (Victorious Theme).mp3"

THEN GOT A CLEAN RUn
Checking library...
 - Checking all tags against filenames..
 - Checking all tags for unwanted/missing info...
 - Checking all tags for duplicates...
 - Checking Artists folder...
 - Checking Miscellaneous Songs folder...
 - Checking Musivation folder...
 - Checking Motivation folder...

TODO: 
check over recent C:\Users\David\GitHubRepos\AudioMirror commits to see how structure changed! 
update this whole doc as more of a record of what happened , in chronolgical order,  pre applied should be at top 

e06a11a0d77e560c085afc656cbc3d93cbc943b3 = 'Ain't Thinkin' 'Bout You' -> Remove version from title = also did this, libchecker missed this!  add task to fix this in libchecker!

after added new songs to itunes, synced iphone = need reminder for this 

---

## Pre-Applied (already done via mp3tag)

- `TCMP = 1` on all 20 tracks
- Filename format: `Artist - Title.mp3` (or `Artist1;Artist2 - Title.mp3`)
- "feat" removed from titles and filenames

---

## Artists - New Folder

### Red Hot Chili Peppers (3 songs - all from "Californication" - threshold met)

| Filename | Destination |
|---|---|
| `Red Hot Chili Peppers - Californication.mp3` | `Artists/Red Hot Chili Peppers/Californication/` *(new)* |
| `Red Hot Chili Peppers - Otherside.mp3` | `Artists/Red Hot Chili Peppers/Californication/` *(new)* |
| `Red Hot Chili Peppers - Scar Tissue.mp3` | `Artists/Red Hot Chili Peppers/Californication/` *(new)* |

---

## Artists - Existing Folder

### mike. (folder is `mike`, not `mike.`)

| Filename | Destination |
|---|---|
| `mike. - north star.mp3` | `Artists/mike/Singles/` |

---

## Films (new folder)

### Kabhi Khushi Kabhie Gham (2001 Bollywood film)

| Filename | Destination |
|---|---|
| `Jatin-Lalit;Amit Kumar;Sonu Nigam;Alka Yagnik;Udit Narayan;Kavita Krishnamurthy - Bole Chudiyan.mp3` | `Sources/Films/Kabhi Khushi Kabhie Gham/` *(new)* |
| `Jatin-Lalit;Lata Mangeshkar - Kabhi Khushi Kabhie Gham.mp3` | `Sources/Films/Kabhi Khushi Kabhie Gham/` *(new)* |

---

## Miscellaneous Songs (13 tracks)

| Filename |
|---|
| `adore - did i tell u that i miss u.mp3` |
| `Big Mountain - Baby, I Love Your Way.mp3` |
| `Bow Wow;Chris Brown - Ain't Thinkin' 'Bout You (Explicit Version).mp3` |
| `Cutting Crew - (I Just) Died In Your Arms.mp3` |
| `Djo - End of Beginning.mp3` |
| `Donna Lewis - I Love You Always Forever.mp3` |
| `Hoodie Allen - No Faith in Brooklyn.mp3` |
| `Lynyrd Skynyrd - Simple Man.mp3` |
| `Lynyrd Skynyrd - Sweet Home Alabama.mp3` |
| `Milky - Just The Way You Are.mp3` |
| `Rusted Root - Send Me On My Way.mp3` |
| `The Goo Goo Dolls - Iris.mp3` |
| `The Goo Goo Dolls - Slide.mp3` |
| `Victorious Cast;Victoria Justice - Make It Shine (Victorious Theme).mp3` |


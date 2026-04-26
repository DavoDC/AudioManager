# New Music Integration Plan - 2026-04-07 (Batch B)

> **Status: COMPLETE - clean lib checker run achieved.**

---

## Pre-Applied (via mp3tag before integration)

- `TCMP = 1` on all 20 tracks
- Filename format: `Artist - Title.mp3` / `Artist1;Artist2 - Title.mp3`
- "feat" removed from titles and filenames

---

## File Routing

| Filename | Destination | Notes |
|---|---|---|
| `Red Hot Chili Peppers - Californication.mp3` | `Artists/Red Hot Chili Peppers/Californication/` | New artist folder - 3 songs from same album |
| `Red Hot Chili Peppers - Otherside.mp3` | `Artists/Red Hot Chili Peppers/Californication/` | |
| `Red Hot Chili Peppers - Scar Tissue.mp3` | `Artists/Red Hot Chili Peppers/Californication/` | |
| `mike. - north star.mp3` | `Artists/mike/Singles/` | Existing folder (`mike` not `mike.`) |
| `Jatin-Lalit;...;Kavita Krishnamurthy - Bole Chudiyan.mp3` | `Sources/Films/Kabhi Khushi Kabhie Gham/` | New Films folder |
| `Jatin-Lalit;Lata Mangeshkar - Kabhi Khushi Kabhie Gham.mp3` | `Sources/Films/Kabhi Khushi Kabhie Gham/` | |
| `The Goo Goo Dolls - Iris.mp3` | `Artists/The Goo Goo Dolls/Dizzy up the Girl/` | Threshold met (3 songs in Misc) |
| `The Goo Goo Dolls - Slide.mp3` | `Artists/The Goo Goo Dolls/Dizzy up the Girl/` | Both from same album |
| `Victorious Cast;Victoria Justice - Make It Shine (Victorious Theme).mp3` | `Sources/Shows/Victorious/` | Initially to Misc, corrected to Shows |
| `adore - did i tell u that i miss u.mp3` | `Miscellaneous Songs/` | |
| `Big Mountain - Baby, I Love Your Way.mp3` | `Miscellaneous Songs/` | |
| `Bow Wow;Chris Brown - Ain't Thinkin' 'Bout You.mp3` | `Miscellaneous Songs/` | "(Explicit Version)" removed from title post-move |
| `Cutting Crew - (I Just) Died In Your Arms.mp3` | `Miscellaneous Songs/` | |
| `Djo - End of Beginning.mp3` | `Miscellaneous Songs/` | |
| `Donna Lewis - I Love You Always Forever.mp3` | `Miscellaneous Songs/` | |
| `Hoodie Allen - No Faith in Brooklyn.mp3` | `Miscellaneous Songs/` | |
| `Lynyrd Skynyrd - Simple Man.mp3` | `Miscellaneous Songs/` | |
| `Lynyrd Skynyrd - Sweet Home Alabama.mp3` | `Miscellaneous Songs/` | |
| `Milky - Just The Way You Are.mp3` | `Miscellaneous Songs/` | |
| `Rusted Root - Send Me On My Way.mp3` | `Miscellaneous Songs/` | |

---

## LibChecker Run 1 - Issues Found

```
 - Found 'feat.' in album of 'Victorious Cast;Victoria Justice - Make It Shine (Victorious Theme)'
 - Found 'original' in album of 'Jatin-Lalit;...;Kavita Krishnamurthy - Bole Chudiyan'
 - Found 'soundtrack' in album of 'Jatin-Lalit;...;Kavita Krishnamurthy - Bole Chudiyan'
 - Found 'original' in album of 'Jatin-Lalit;Lata Mangeshkar - Kabhi Khushi Kabhie Gham'
 - Found 'soundtrack' in album of 'Jatin-Lalit;Lata Mangeshkar - Kabhi Khushi Kabhie Gham'
 - Total hits: 5
 - There are 3 songs by 'The Goo Goo Dolls' in the Miscellaneous Songs folder!
 - Total hits: 1
```

### Fixes Applied

| # | Fix |
|---|---|
| 1 | Victorious: moved to `Sources/Shows/Victorious/`, album set to `Victorious OST` |
| 2 | Bole Chudiyan: album updated to `Kabhi Khushi Kabhie Gham OST` |
| 3 | Kabhi Khushi Kabhie Gham: album updated to `Kabhi Khushi Kabhie Gham OST` |
| 4 | Goo Goo Dolls: all 3 songs moved to `Artists/The Goo Goo Dolls/Dizzy up the Girl/` + `Singles/` |

---

## LibChecker Run 2 - Clean

```
Checking library...
 - Checking all tags against filenames..
 - Checking all tags for unwanted/missing info...
 - Checking all tags for duplicates...
 - Checking Artists folder...
 - Checking Miscellaneous Songs folder...
 - Checking Musivation folder...
 - Checking Motivation folder...
```


# New Music Integration Plan - 2026-04-07 (Batch B)

> **Status: READY TO EXECUTE** - All decisions resolved.

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

---

## Execution Steps

1. Create `C:\Users\David\Audio\Artists\Red Hot Chili Peppers\Californication\`
2. Move 3 RHCP files there
3. Move `mike. - north star.mp3` to `C:\Users\David\Audio\Artists\mike\Singles\`
4. Create `C:\Users\David\Audio\Sources\Films\Kabhi Khushi Kabhie Gham\`
5. Move 2 Bollywood files there
6. Move remaining 13 files to `C:\Users\David\Audio\Miscellaneous Songs\`
7. Verify Downloads\NewMusic is now empty

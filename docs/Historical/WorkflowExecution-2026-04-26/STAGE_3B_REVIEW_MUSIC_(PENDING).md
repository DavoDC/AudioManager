# STAGE 3B: Review New Music - Listen & Verify

**Status: COMPLETE** - Quality control check before real integration (51 tracks approved)

---

## BEFORE STATE - Complete Inventory (Start of Review)

**Date scanned:** 2026-04-28
**Total tracks:** 112

### Albums (5 total, 82 tracks)
1. **Kings Dead - Revenge of the Beast** (16 tracks)
   - 01-16: [all tracks present]
2. **Lupe Fiasco - Lasers** (12 tracks)
   - 01-12: [all tracks present]
3. **Lupe Fiasco - Lupe Fiasco's Food & Liquor** (16 tracks)
   - 01-16: [all tracks present]
4. **Lupe Fiasco - Lupe Fiasco's The Cool** (19 tracks)
   - 01-19: [all tracks present]
5. **Shaggy - The Boombastic Collection - Best Of Shaggy (International Version)** (19 tracks)
   - 01-19: [all tracks present]

### Single Files (28 tracks)
- Akira The Don - UNSTOPPABLE.mp3
- cafune - Tek It.mp3
- Dizzy Wright - Loophole (feat. Nowdaze).mp3
- Dylan Owen - Evergreen Nights.mp3
- Dylan Owen - The Glory Years.mp3
- Dylan Owen - The Window Seat.mp3
- Eels - Mighty Fine Blues.mp3
- Eyedress - Something About You.mp3
- Guy Sebastian - Battle Scars (feat. Lupe Fiasco).mp3
- Joshua Golden - 143.mp3
- Joshua Golden - heaven come.mp3
- Joshua Golden - used to.mp3
- Kings Dead - All My Life (feat. Alexus Lee).mp3
- Lupe Fiasco - Dots & Lines.mp3
- Lupe Fiasco - Samurai.mp3
- mike. - play my hand.mp3
- mike. - real things.mp3
- Ravyn Lenae - Love Me Not.mp3
- Shaggy - Keep'n It Real.mp3
- Sig Roy - Don't Let Me Down.mp3
- Sig Roy - pull up.mp3
- Sig Roy - Together.mp3
- Surf Mesa - Another Life.mp3
- Temper City - Self Aware.mp3
- The Marías - No One Noticed.mp3
- Victoria Justice - RAW.mp3
- Witt Lowry - Put Me First (feat. Joshua Golden).mp3

### Loose Files (2 tracks)
- 05 - Sail Up The Sun.mp3 (from Dylan Owen - There's More To Life)
- 08 - There's More To Life.mp3 (from Dylan Owen - There's More To Life)

---

## Purpose

Listen to and verify all tracks in `C:\Users\David\Downloads\NewMusic\` before integrating into the library. This is a critical quality gate - sometimes whole albums get added but may have tracks you don't want. Better to remove unwanted songs now than after integration.

---

## Checklist

- [x] Play through new music folder (or spot-check key tracks)
- [x] Check for complete albums - verify all tracks you want are present
- [x] Remove any songs you don't actually want to keep
- [x] Note any tracks needing special handling (covers, remixes, live versions)
- [x] Confirm you want all remaining tracks in library
- [x] Count final tracks before proceeding to Stage 3C

---

## Current Status

**BASELINE SNAPSHOT (start of review):**
- **Total tracks found:** 112
  - Albums (5): Kings Dead Revenge (16), Lupe Fiasco Lasers (12), Lupe Food & Liquor (16), Lupe The Cool (19), Shaggy Boombastic Collection (19)
  - Singles/loose files: 28

**Current running total:** 101
- Removed: 2 instrumentals (Akira The Don)
- Removed: 6 tracks (Dylan Owen - There's More To Life, personal preference)
- Removed: 1 single (Dylan Owen - The Glory Years, personal preference)
- Removed: 2 loose tracks already moved to PROCESSED (Dylan Owen - 05, 08)

**Organization method:** Reviewed albums moved to `C:\Users\David\Downloads\NewMusic\PROCESSED\` for tracking. Integrator scans recursively, so location does not affect processing.

**Albums being reviewed:**

### Akira The Don - THE SHINING OF BEING
- **Source folder:** `C:\Users\David\Downloads\NewMusic\Akira The Don - THE SHINING OF BEING\`
- **Library destination:** `C:\Users\David\Audio\Musivation\Akira The Don\[Person]\The Shining of Being\`
- **Decision:** KEEP (album version preferred over any existing singles)
- **Action:** Delete instrumental - `06 - THE SHINING OF BEING (Instrumental).mp3`
- **Tracks verified:**
  1. 01 - THE SHINING OF BEING.mp3
  2. 02 - WHAT YOU ARE LOOKING FOR IS WHAT YOU ARE.mp3
  3. 03 - INTO THE INFINITE.mp3
  4. 04 - I Had to Become Like You.mp3
  5. 05 - Loving Indifference.mp3
- **Filename format:** Integrator will rename to "Akira The Don - [Title]" per Music lib rules
- **Tracks removed:** 1 instrumental
- **Tracks from this album:** 5 remaining

### Akira The Don - WHAT IF_
- **Source folder:** `C:\Users\David\Downloads\NewMusic\Akira The Don - WHAT IF_\`
- **Library destination:** `C:\Users\David\Audio\Musivation\Akira The Don\[Person]\What If\`
- **Decision:** KEEP
- **Action:** Delete instrumental - `03 - WHAT IF_ (Instrumental).mp3`
- **Tracks verified:**
  1. 01 - WHAT IF_.mp3
  2. 02 - AUTHOR YOURSELF.mp3
- **Tracks removed:** 1 instrumental
- **Tracks from this album:** 2 remaining

### Dylan Owen - There's More To Life
- **Source folder:** `C:\Users\David\Downloads\NewMusic\Dylan Owen - There's More To Life\`
- **Decision:** KEEP (with track removals)
- **Tracks KEPT:**
  - `05 - Sail Up The Sun.mp3`
  - `08 - There's More To Life.mp3`
- **Tracks DELETED:**
  - `01 - The Glory Years.mp3`
  - `02 - Everything Gets Old.mp3`
  - `03 - The Best Fears of Our Lives.mp3`
  - `04 - The Streets.mp3`
  - `06 - Land of the Brave.mp3`
  - `07 - This Incredible Life.mp3`
- **Tracks removed:** 6 total
- **Tracks from this album:** 2 remaining

### Dylan Owen - The Glory Years
- **Source folder:** `C:\Users\David\Downloads\NewMusic\Dylan Owen - The Glory Years\`
- **Decision:** DELETE (entire album)
- **Reason:** Personal preference
- **Status:** Deleted

---

## Album Integration Rules

**Rule 1 - Album preference:** If singles exist in library + full album version is available, DELETE singles and KEEP ALBUM. Tracks remain the same musically but are now in album context instead of single context.

**Rule 2 - Akira The Don instrumentals (HIGH PRIORITY):** Auto-delete any track matching: Artist = "Akira The Don" AND filename contains "Instrumental"

---

## Action required:** Continue through remaining folders, noting album decisions and track removals.

---

## AFTER STATE - Final Inventory (After Review Complete)

**Status:** COMPLETE - All tracks processed and moved to PROCESSED folder

**Total tracks after deletion:** 51
**Total tracks removed:** 61
**Removal rate:** 54.5% of baseline

### Albums kept (7 total, 26 tracks):

**Singles/loose files kept (25 tracks)** - see details below
1. **Akira The Don - THE SHINING OF BEING** (5 tracks) - 01, 02, 03, 04, 05 (removed instrumental)
2. **Akira The Don - WHAT IF_** (2 tracks) - 01, 02 (removed instrumental)
3. **Kings Dead - Revenge of the Beast** (5 tracks) - 01, 02, 04, 06, 11
4. **Lupe Fiasco - Lasers** (4 tracks) - 03, 06, 07, 12
5. **Lupe Fiasco - Lupe Fiasco's Food & Liquor** (3 tracks) - 06, 09, 10
6. **Lupe Fiasco - Lupe Fiasco's The Cool** (2 tracks) - 05, 09
7. **Shaggy - The Boombastic Collection** (5 tracks) - 01, 03, 08, 09, 11

### Singles/loose files kept (25 tracks):
- Akira The Don - UNSTOPPABLE.mp3
- cafune - Tek It.mp3
- Dizzy Wright - Loophole (feat. Nowdaze).mp3
- Dylan Owen - Evergreen Nights.mp3
- Dylan Owen - The Window Seat.mp3
- Eels - Mighty Fine Blues.mp3
- Eyedress - Something About You.mp3
- Guy Sebastian - Battle Scars (feat. Lupe Fiasco).mp3
- Joshua Golden - 143.mp3
- Joshua Golden - heaven come.mp3
- Joshua Golden - used to.mp3
- Lupe Fiasco - Dots & Lines.mp3
- mike. - play my hand.mp3
- mike. - real things.mp3
- Ravyn Lenae - Love Me Not.mp3
- Shaggy - Keep'n It Real.mp3
- Sig Roy - Don't Let Me Down.mp3
- Sig Roy - Together.mp3
- Surf Mesa - Another Life.mp3
- Temper City - Self Aware.mp3
- The Marías - No One Noticed.mp3
- Victoria Justice - RAW.mp3
- Witt Lowry - Put Me First (feat. Joshua Golden).mp3
- 05 - Sail Up The Sun.mp3 (Dylan Owen - There's More To Life)
- 08 - There's More To Life.mp3 (Dylan Owen - There's More To Life)

### Albums removed (partial):
- **Lupe Fiasco - Lasers:** 8 of 12 tracks removed (kept 4)
- **Lupe Fiasco - Lupe Fiasco's Food & Liquor:** 13 of 16 tracks removed (kept 3)
- **Lupe Fiasco - Lupe Fiasco's The Cool:** 17 of 19 tracks removed (kept 2)
- **Kings Dead - Revenge of the Beast:** 11 of 16 tracks removed (kept 5)
- **Shaggy - The Boombastic Collection:** 14 of 19 tracks removed (kept 5)

### Albums removed (complete):
- **Dylan Owen - The Glory Years** (entire album removed)
- **Dylan Owen - There's More To Life** (6 of 8 tracks removed, kept 2 loose files)

---

## Readiness for Stage 3C

**All music processed and ready for integration.** 52 tracks staged in `C:\Users\David\Downloads\NewMusic\PROCESSED\` ready to route into Audio library.

Next step: Implement decision logging feature (TIER 1 prerequisite), then execute Stage 3C integration with `--dry-run` first.


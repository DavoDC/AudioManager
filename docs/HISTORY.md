# History

Completed features, settled design decisions, and resolved tasks.

---

## 2026-04-08 - LibChecker: version/edition suffix detection

Added `"version"` and `"explicit"` to `Constants.UnwantedInfo`. LibChecker now flags titles containing `(Explicit Version)`, `(Album Version)` etc. `(Radio Edit)` and `(Deluxe Edition)` were already caught by the existing `"edit"` entry.

---

## Constants.cs consolidation (already done)

`MiscDir`, `ArtistsDir`, `MusivDir`, `MotivDir` were already defined in `Constants.cs`. Both `LibChecker` and `MusicIntegrator` already read from `Constants` - no duplication existed.

---

## LibChecker owns validation (already done)

`MusicIntegrator`'s only "validation" is a routing precondition (skip files missing artist/title - can't determine destination without them). This is not duplicated LibChecker logic. The boundary was already clean.

---

## ReportWriter - plain static class (already done)

`ReportWriter` is already `internal static class`, not inheriting from Doer. Idea retired.

---

## Analyser stats (already implemented)

Total playback hours, average and median song length, total library size (GB), average file size (MB), track age stats (average, median, oldest, newest), year and decade frequency distribution. All in `Analyser.cs`.

---

## 2026-04-07 - Fix git folder casing

Git tracked folders in old uppercase names (`PROJECT/`, `REPORTS/`, `Docs/`) while they were lowercase on disk. Fixed via two-step `git mv` per folder (required because Windows filesystem is case-insensitive and ignores direct renames). Also updated `REPORTS` path string in `Constants.cs` and comments in `ReportWriter.cs` to match.

# History

Completed features, settled design decisions, and resolved tasks.

---

## ReportWriter - plain static class (already done)

`ReportWriter` is already `internal static class`, not inheriting from Doer. Idea retired.

---

## Analyser stats (already implemented)

Total playback hours, average and median song length, total library size (GB), average file size (MB), track age stats (average, median, oldest, newest), year and decade frequency distribution. All in `Analyser.cs`.

---

## 2026-04-07 - Fix git folder casing

Git tracked folders in old uppercase names (`PROJECT/`, `REPORTS/`, `Docs/`) while they were lowercase on disk. Fixed via two-step `git mv` per folder (required because Windows filesystem is case-insensitive and ignores direct renames). Also updated `REPORTS` path string in `Constants.cs` and comments in `ReportWriter.cs` to match.

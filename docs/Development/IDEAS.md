# Ideas & Future Work

Single source of truth for all pending work. **CLI only.** GUI planning: `GUI-ROADMAP.md`. Completed items -> `HISTORY.md`.

Items are tiered by priority. Do not advance to the next tier until the current tier is verified on real data.

---

## TIER 1 - MVP

**Goal: validate the integration pipeline on real data.** All safety prerequisites are in place. These items improve the duplicate detection and routing workflow before the first real integration run.

**PRIORITY: Duplicate detection UX batch - interrelated items, high impact on integration workflow**

- [ ] **UX: Add succinct routing summary line for quick decision-making** - Currently the routing proposal shows the full destination path + reason, which is good for diagnostics but slow to scan. Add a short one-liner above the full path that gives just the key routing decision (e.g. `-> Dizzy Wright / Singles`). User checks the summary for quick Y/N, reads the full path only when something looks wrong. Both lines shown always - summary for speed, path for diagnostics.

- [ ] **UX: Detect compilation vs artist album for smarter [L] recommendation** - Current logic only recommends [L] when `relMirrorPath` starts with `Compilations\` (root folder only). Misses: library track in a named compilation series that lives elsewhere (e.g. `Musivation\Akira The Don\MEANINGWAVE MASTERPIECES V\`). Better approach: derive the album folder from `duplicatePath` (`Path.GetDirectoryName(duplicatePath)`), then read ALL XML files in that folder and collect the full `Artists` field from each (not just primary artist - use the whole field). If many distinct artists appear across the tracks, it is a compilation. If the same artist appears on every track, it is an artist album. Rule: if library album is detected as compilation AND new file has a named artist album (`newIsAlbum`), recommend [L] with reason "Library copy is from a compilation; new file is from artist album". No keyword lists - ground truth from the mirror data.

- [ ] **UX: Consolidate duplicate decisions together in output** - Currently routing decisions and duplicate decisions are interspersed, making it hard to review all duplicates together. Group all duplicate detection decisions together (before or after routing decisions) so users can context-switch once instead of repeatedly alternating between duplicate and routing reviews.

---

## TIER 2 - QUALITY

**Goal: improve user experience for real integration and add minimal test coverage for high-risk code paths.**

- [ ] **UX: Misc routes should not auto-accept - review all at end instead** - Auto-accept has been turned off (user now confirms Y/N like all other routes). Still need: collect all Misc-routed files during the run, then present them all at once at the end as a single batch review ("These N files would go to Misc - accept all / review one by one / decline all?"). This way Misc routing is visible and auditable without interrupting the flow for every file. Needs investigation of current state + implementation of batch review at end.

- [ ] **TagFixer: extend genre handling for additional artists** - Currently TagFixer sets genre to "Musivation" only for Akira The Don. Expand to:
  - **Loot Bryon Smith** -> Genre = "Musivation" (per Music-Library-Rules.md spec)
  - **Generic "Motivation" tracks** -> Genre = "Motivation" (currently not handled)
  - Current implementation: `ShouldFixGenre()` and `DetermineGenre()` in TagFixer.cs need extension to check artist name and/or existing genre tags. Once implemented, TagFixer will be 100% comprehensive.

- [ ] **Performance investigation - Parser is slow** - Analysis runs take time; Parser phase is noticeably slow when processing 5000+ MP3s. Profile the bottleneck: is it XML parsing, tag reading, file I/O, or something else? Benchmark against alternative approaches (e.g., streaming vs. loading entire mirror into memory, parallel processing per artist folder, etc.). Document findings and propose optimization target for TIER 2 implementation (if payback is clear).

- [ ] **Add minimal automated tests (scoped down from kitchen-sink)** - ROI analysis showed full test suite not worth it; these three are.
  - **Smoke build test** (~50 lines): MSBuild builds cleanly; exe runs with `--help` without crashing. Catches today's class of bug. Payback in weeks.
  - **`GetDestDir()` routing tests** (~150 lines): fixed inputs (artist, album, scan-ahead set) -> fixed destinations. Routing is the most dangerous code path - wrong dest = real files moved wrong. Payback in 1-2 months.
  - **`PreProcessTags()` tag-mutation tests** (~80 lines): no-TCMP input -> TCMP=True; Akira The Don wrong-genre input -> Musivation. Tiny and easy.
  - **Skipped:** full LibChecker-rule suite (~400+ lines, 6-12 month payback - add incrementally when rules change, don't backfill). Full integration test with fake MP3s (too heavy, dry-run already covers).
  - Add `AudioManager.Tests` xUnit project; wire into `launch.bat` as "Run tests" menu item; broken tests block exe launch.

---

## TIER 3 - POLISH

**Goal: close structural gaps and improve developer experience. Non-blocking.**

- [ ] **Fix plural "songs" when count is 1** - e.g. "Singles (1 songs from Brian Tracy)" should say "1 song". Grep for the format string generating this output and add a ternary for singular/plural. Low priority.

- [ ] **Deep dive: audit full library against Music-Library-Rules.md** - scan AudioMirror XMLs, cross-reference every track against the rules doc, produce a violations/gaps report. Then confirm LibChecker catches everything the doc mandates. Goal: a clean LibChecker run means full conformance.
  - *Partial progress (2026-04-09)*: rules gap analysis done. Added `CheckAlbumSubfolderRule()` and `CheckGenreVsFolder()`. Remaining: run LibChecker on the full library, scan AudioMirror XMLs for violations not caught by LibChecker.

- [ ] **Centralise rules - one system for both integration routing and LibChecker** - currently routing rules (artist -> folder mapping, genre overrides, special cases like ATD) and LibChecker validation rules live in separate places. Refactor into a single `RulesEngine` concept: rules defined once, consumed by both MusicIntegrator (routing decisions) and LibChecker (validation). Benefits: add one rule, both systems honour it; no risk of the two diverging. Also: audit for missing ATD (Akira The Don) rules - suspected gap between what TagFixer sets and what LibChecker checks.

- [ ] **Simplify launch.bat - move menu logic into Program.cs** - RivalsVidMaker's `run.bat` is 2 lines; all menu/mode logic lives in Python. AudioManager's `launch.bat` is 91 lines with its own menu, duplicating CLI arg logic already in `Program.cs`. Refactor: `launch.bat` becomes a ~5-line wrapper that just builds + runs `AudioManager.exe` with no args; all menu logic lives in Program.cs where it's testable and debuggable.

- [ ] **Auto-migrate existing Misc songs when scan-ahead promotes an artist** - currently flagged for MANUAL migration (deemed too risky to auto-move existing library files). Revisit with a confirmation gate after tests exist.

- [ ] **Sources/ routing not implemented in GetDestDir()** - `Constants.SourcesDir` exists but `MusicIntegrator.GetDestDir()` has no routing logic for Sources/Films, Sources/Shows, or Sources/Anime. Films/Shows/Anime tracks currently fall to Misc and require manual folder-picker redirection. `Music-Library-Rules.md` documents the expected routing rules (Films subfolder = film name, Shows subfolder = show name, Anime = separate).
  - **Challenge:** Automating this is difficult - metadata alone rarely indicates source type. Current approach: if `Album` contains "OST" or "Soundtrack", prompt user for subfolder choice rather than defaulting to Misc.
  - **Alternative approach:** Study existing Sources/Films, Sources/Shows, Sources/Anime tracks for metadata patterns (album names, artist names, genre, etc.) that distinguish them. May reveal rough heuristics for auto-detection, or may confirm that folder-picker prompt is the only reliable option.
  - **Recommendation:** TIER 4 exploratory task - audit existing metadata patterns before deciding if automation is feasible. May need to accept manual folder-picker as permanent solution.

- [ ] **Audit libchecker-exceptions.xml - distinguish bug fixes from genuine exceptions** - review all exceptions to identify which are workarounds for LibChecker regex bugs (e.g., "version" in legitimate titles like Alan Watts tracks) vs. genuine tracks that need exemption (e.g., Eric Thomas bonus content). For each regex-bug workaround, ensure there's a corresponding TIER 1 code improvement in IDEAS. Goal: exceptions.xml contains only track-specific needs, not rule improvements. Low priority.

- [ ] **AudioMirrorCommitter safety check - prevent commit on ANY issues, not just LibChecker** - currently skips auto-commit only if LibChecker reported hits, but doesn't account for other issues like unexpected file extensions (discovered during Reflector mirror phase). Update check: if Reflector found unexpected extensions OR LibChecker reported hits, skip commit and notify user. Goal: auto-commit only runs when entire pipeline is completely clean.

- [ ] **Re-enable AudioMirrorCommitter auto-commit** - currently disabled (shows manual instructions instead). Re-enable when program is more mature and proven stable on real library operations. Prerequisites: (1) TIER 1 all verified, (2) several weeks of stable runs with zero accidental data loss, (3) both safety checks above in place. Low priority - manual commits are safe and auditable.
  - **Note:** Old auto-commit logic is commented out in `AudioMirrorCommitter.TryCommit()` (lines ~60-85) for easy re-enable.

- [ ] **Fix report table formatting - markdown tables instead of plain text** - Current reports (`reports/2026/YYYY-MM-DD - AudioReport.md`) render statistics tables as plain text with no formatting. Modify `ReportWriter.cs` (and/or `Analyser.cs` if it generates the data) to emit proper markdown tables with pipe-delimited columns and header separators. Affects readability and consistency with GitHub markdown rendering.

- [ ] **Low priority: Evaluate removing .md integration logs in favour of decision XMLs** - Integration logs (`logs/integration-*.md`) and decision XMLs overlap in content. If XMLs contain all routing information, the .md logs may be redundant. Evaluate what .md logs have that XMLs don't (human-readable narrative vs structured data). If XMLs are sufficient for audit and analysis, remove .md logs to simplify the output.

- [ ] **Low priority idea: Routing decision analysis mode** - Add a mode that reads decision XMLs, cross-references routing decisions against routing rules code and LibChecker rules, and flags inconsistencies. Produces a report: "these N files were routed to X but LibChecker would flag them as Y". Pairs well with the "Centralise rules" refactor. Exploratory - assess value after the first real integration run produces decision XML data to analyse.

---

## TIER 4 - FUTURE

**Goal: exploratory features and advanced enhancements, tackled after core tiers are stable.**

- **"My Edits" tracking** - detect locally edited songs by comparing duration to official track (>3-4s diff = protected from overwrite).
- **Parody/original song pairing detection** - flag songs where a parody and its original are both in the library.
- **Album completion detection** - cross-reference library against Spotify/MusicBrainz; flag where 50%+ of an album is owned.
- **Fuzzy artist name matching** - handle artist name variations during routing ("The Beatles" vs "Beatles", featured artist formatting differences). Only matters at scale.
- **Fuzzy duplicate title matching** - extend the pre-integration duplicate check to catch near-matches (e.g. "Song (feat. X)" vs "Song", "Song - Remix" vs "Song"). Approach: normalise both sides before comparison by stripping featured artist parentheticals, stripping remix/edit/version suffixes, collapsing whitespace. Blocked by: the exact-match duplicate check must be in place first.

---

## Parked / Deprioritized

- **DECISION GATE: Python Rewrite vs .NET 8 Migration** - AudioManager is .NET Framework; could migrate to .NET 8 SDK-style (lower cost, same language, taglib#-compatible) or rewrite in Python (no build step, lightweight deps via `mutagen`). **Status:** Parked. Program works well; no immediate need to decide. Revisit only if .NET becomes a blocker or if Python advantages outweigh rewrite cost. Decision factors: token cost estimate, confirm Python libs cover TagLib# use cases, ensure file I/O scope is matched.

- **Review mode - library pruning / song-by-song decision tracking** - Add a new `review` mode that walks every song one by one, shows context (tags, folder, optional play count / popularity / lyrics), and asks: keep / remove / defer. Every decision is persisted to a config file (e.g. `config/review-decisions.xml` or similar) with: song fingerprint (artist + title or file hash), decision, date, reason. Old decisions are re-surfaced periodically (e.g. after 12 months) so the review is not one-shot - tastes change, a "keep" today may be a "remove" next year.
  - **Removal candidate signals** (surfaced in review mode to guide decisions, not auto-removed):
    - **Low play count** - cross-reference against iTunes, Last.fm, and/or Spotify listening history. A song never played in 2+ years is a prime candidate.
    - **Negative lyrical/emotional tone** - screen lyrics (via a lyrics API or local cache) and flag songs with heavily negative/depressive/aggressive content on the theory that repeated subconscious exposure shapes mood. This is subjective and must stay advisory, not automated.
  - **Auditable, reversible:** decisions live in a committed config file; a "remove" decision is the review-mode verdict, not an immediate file delete. Actual removal is a separate explicit step after review is complete, with dry-run first.
  - **Status:** Deprioritized. Revisit after core integration pipeline is stable and you have operational experience with large libraries.

---

## See Also

- `docs/Development/HISTORY.md` - completed features, settled design decisions, parked ideas
- `docs/Development/GUI-ROADMAP.md` - GUI planning: webapp, tabs, Sonarr/Radarr vision, far-future integrations
- `docs/References/Music-Library-Rules.md` - canonical rules for library structure
- `docs/Historical/NewMusic-Integration-Plan-20260308.md` - past batch integration (March 2026 batch A)
- `docs/Historical/NewMusic-Integration-Plan-20260407.md` - past batch integration (April 2026)
- `docs/References/AudioMirror-Format.md` - AudioMirror XML format and repo info

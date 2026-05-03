# Ideas & Future Work

Single source of truth for all pending work. **CLI only.** GUI planning: `GUI-ROADMAP.md`. Completed items -> `HISTORY.md`.

Items are tiered by priority. Do not advance to the next tier until the current tier is verified on real data.

---

## TIER 1 - CURRENT BLOCKERS (fix before retrying integration)

**Goal: complete the first real integration cleanly.** ~35 of 50 songs integrated before crashing. LibChecker was clean before this run - all failures below were introduced by this integration and must be resolved before retry.

---

### Blocker A: Integration crash - illegal characters in path

**Integration failed midway (2026-05-03 23:30).** ~35 of 50 songs integrated successfully before crashing on this file.

**Evidence:**
```
[23:30:13] Error processing file: Akira The Don;Scott Adams - AUTHOR YOURSELF.mp3
[23:30:13] Error details: Illegal characters in path.
[23:30:13] Full path: ...\Akira The Don - WHAT IF_\Akira The Don;Scott Adams - AUTHOR YOURSELF.mp3
```

**Root cause:** UNKNOWN - investigate first. Other semicolon-filename songs integrated fine, so semicolon is NOT the culprit. **Primary suspect: question mark in album name "WHAT IF?"** - question marks are illegal in Windows paths. Verify before assuming.

**Fix steps:**
1. Identify the exact illegal character (compare against successfully-integrated files)
2. Extend TagFixer to strip/replace illegal Windows characters (`< > : " / \ | ? *`) from filenames and folder names
3. Add pre-integration validation: scan all files + destination paths before starting, abort if illegal characters found (fail fast, not midway)
4. Re-tag files and retry

---

### Blocker B: Post-integration LibChecker failures

**After partial integration + force regen, LibChecker reported 2 errors. LibChecker was clean before this run - these were introduced by this integration.**

**Failure 1: "soundtrack" in album tag - Eels - Mighty Fine Blues**
```
Found 'soundtrack' in album of 'Eels - Mighty Fine Blues'  (Total hits: 1)
```
- TagFixer [SKIPPED] this file but the album name contains "soundtrack", which LibChecker flags
- Investigate: is this a legitimate album name (false positive -> add to exceptions) or a tag that needs fixing (TagFixer gap)?

**Failure 2: Single-song routed to album subfolder - Mike. - real things**
```
'\Artists\mike\the highs\Mike. - real things.xml': only 1 song from album 'the highs.' but in subfolder 'the highs/' (should be Singles/)
```
- Rule: 1 song from an album -> Singles/; 2+ songs from same album -> album subfolder. This was violated.
- Investigate: routing logic bug in `GetDestDir()` for this edge case, or a tag data issue that fooled the scan-ahead count?

---

## TIER 2 - QUALITY

**Goal: improve UX and add minimal test coverage. Start after first clean integration completes.**

- [ ] **Auto-migrate existing Misc songs when scan-ahead promotes an artist** - currently flagged for MANUAL migration (deemed too risky to auto-move existing library files). Revisit with a confirmation gate after tests exist.

- [ ] **UX: Misc routes should not auto-accept - review all at end instead** - Auto-accept has been turned off (user now confirms Y/N like all other routes). Still need: collect all Misc-routed files during the run, then present them all at once at the end as a single batch review ("These N files would go to Misc - accept all / review one by one / decline all?"). This way Misc routing is visible and auditable without interrupting the flow for every file. Needs investigation of current state + implementation of batch review at end.

- [ ] **TagFixer: extend genre handling for additional artists** - Currently TagFixer sets genre to "Musivation" only for Akira The Don. Expand to:
  - **Loot Bryon Smith** -> Genre = "Musivation" (per Music-Library-Rules.md spec). **Full reasoning:** ALL FILES in the Musivation folder must have the Musivation genre tag. Loot Byron Smith is a Musivation artist, so files are routed to the Musivation folder. Because they go to Musivation, they require the Musivation genre tag - it's a folder-level requirement enforced by LibChecker.
  - **Generic "Motivation" tracks** -> Genre = "Motivation" (currently not handled)
  - Current implementation: `ShouldFixGenre()` and `DetermineGenre()` in TagFixer.cs need extension to check artist name and/or existing genre tags. Once implemented, TagFixer will be 100% comprehensive.

- [ ] **Scan-ahead: show progress indicator during computation** - Scan-ahead calculation takes noticeably long (user thought it hung). Currently no output between "Scan-ahead: N artist(s) will hit 3-song threshold:" and the results. Fix: print a progress line while scanning, e.g. "Scanning batch... (checking N artists)" or a simple dot-tick per artist. Evidence from feedback: user saw scan-ahead output then silence for several seconds with no feedback.

- [ ] **Routing proposal UX: split `Proposed:` into human-readable path + filesystem path** - Currently shows one long `Proposed: Musivation\Akira The Don\Singles\...` line that mixes logical path (for human review) and filesystem path (for execution). Suggested improvement: split into two lines:
  - `Proposed: Akira The Don / Singles` (short, human-readable)
  - `Path: Musivation\Akira The Don\Singles\Akira The Don;Brian Tracy - UNSTOPPABLE.mp3` (full filesystem path)
  This makes the proposal easier to read at a glance before accepting.

- [ ] **Routing proposal UX: `Reason` field should not duplicate the proposal** - Currently `Reason` sometimes restates what `Proposed:` already shows (e.g. "Akira The Don -> People/Rupert Spira/THE SHINING OF BEING (5 songs from album)" which mirrors the `->` line above it). Reason should explain WHY the routing was chosen, not restate the destination. Fix: audit Reason strings in routing logic; if a Reason just restates the destination path, replace with the actual decision logic (e.g. "5 songs from album -> album subfolder", "artist folder exists -> auto-route", etc.).

- [ ] **Routing proposal UX: concise proposal positioning** - Concise proposal summary is showing in the wrong position relative to the `Proposed:` line. Needs investigation and discussion with David before implementing (user flagged: "not sure about best way to think about, ask me if needed").

- [ ] **TagFixer output formatting: missing blank line between SKIPPED and FIXED entries** - Tag fixer output has inconsistent spacing. A blank line separates most [FIXED] entries but is missing between a [SKIPPED] line and the following [FIXED] line. Fix: ensure all consecutive [FIXED]/[SKIPPED] blocks are separated by a blank line for consistent readability.

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

- [ ] **Deep dive: audit full library against Music-Library-Rules.md** - scan AudioMirror XMLs, cross-reference every track against the rules doc, produce a violations/gaps report. Then confirm LibChecker catches everything the doc mandates. Goal: a clean LibChecker run means full conformance.
  - *Partial progress (2026-04-09)*: rules gap analysis done. Added `CheckAlbumSubfolderRule()` and `CheckGenreVsFolder()`. Remaining: run LibChecker on the full library, scan AudioMirror XMLs for violations not caught by LibChecker.

- [ ] **Centralise rules - one system for both integration routing and LibChecker** - currently routing rules (artist -> folder mapping, genre overrides, special cases like ATD) and LibChecker validation rules live in separate places. Refactor into a single `RulesEngine` concept: rules defined once, consumed by both MusicIntegrator (routing decisions) and LibChecker (validation). Benefits: add one rule, both systems honour it; no risk of the two diverging. Also: audit for missing ATD (Akira The Don) rules - suspected gap between what TagFixer sets and what LibChecker checks.
  - **Structural fix for tag/validation sync failures:** TagFixer and LibChecker currently have independent, unsynchronised rule sets. This caused a real failure: TagFixer SKIPPED a file ("no fixes needed") but LibChecker later flagged it - because each component only knows its own rules. A shared RulesEngine makes this class of failure structurally impossible: both components consult the same rules, so a file that passes TagFixer provably passes LibChecker.

- [ ] **Simplify launch.bat - move menu logic into Program.cs** - RivalsVidMaker's `run.bat` is 2 lines; all menu/mode logic lives in Python. AudioManager's `launch.bat` is 91 lines with its own menu, duplicating CLI arg logic already in `Program.cs`. Refactor: `launch.bat` becomes a ~5-line wrapper that just builds + runs `AudioManager.exe` with no args; all menu logic lives in Program.cs where it's testable and debuggable.

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

- **Auto-routing for known routing cases** - When a routing decision is certain (perfect match for a known case), skip the human confirmation and route automatically. **EXPLICITLY GATED:** Do not implement until David says "I'm confident enough in the program." David's words: "WAIT until I explicitly say I'm confident enough." Only revisit after several real integration runs with zero incorrect routings and TIER 2 test coverage in place.

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

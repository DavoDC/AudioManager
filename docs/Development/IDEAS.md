# Ideas & Future Work

Single source of truth for all pending work. **CLI only.** GUI planning: `GUI-ROADMAP.md`. Completed items -> `HISTORY.md`.

Items are tiered by priority. Do not advance to the next tier until the current tier is verified on real data.

---

## TIER 1 - BLOCKING

**Goal: deliver auto-routing for known cases - eliminate confirmation fatigue. Prerequisites must be verified on real data first.**

**TIER 1 threshold:** anything that would cause a LibChecker warning belongs here, regardless of where or when it was discovered. Routing gaps, rule divergence, config omissions - all TIER 1 if LibChecker would fire on it.

**ACTIVE FOCUS:** Routing test suite complete (all tests green as of 2026-05-31). All active TIER 1 items resolved in code; remaining TIER 1 items require verification on next real integration batch.

- [ ] **[BLOCKED: do after Raphael trip] Fix MP3 filename casing to match artist ID3 tags** - several files have filenames with old/inconsistent artist casing that diverges from the ID3 tag (e.g., `Bowling For Soup - 1985.mp3` but tag says `Bowling for Soup`; `24kgoldnDaBaby - Coco.mp3` but tag says `24kGoldn`; similarly `Iann Dior` vs `iann dior`, `JAY-Z` vs `Jay-Z`, `Kota the Friend` vs `KOTA The Friend`). Fix via mp3tag on the master machine - rename files to match the ID3 artist tag casing. This WILL fix all current LibChecker casing warnings (genuine mismatches, not false positives). **Must be done before next integration so LibChecker is clean. Raphael has master copy of audio library - do not edit on e15 to avoid sync conflict.**

---

## TIER 2 - QUALITY

**Goal: improve UX, add test coverage, and audit metadata quality.**

- [ ] **Terminal vs markdown output - separate handling needed** - Markdown tables render correctly in `.md` files but look cluttered in terminal (raw pipes/dashes). Options to evaluate via `/think` pass: (A) auto-open the saved `.md` report in the default viewer after analysis (zero extra code, best reading experience); (B) prompt the user to open it (give full path in output); (C) add terminal-native table formatting as an alternative output path. Strong prior: option A or B is better than option C - a markdown viewer is purpose-built for this, adding a terminal table renderer duplicates effort for lower quality. Decision: `/think` pass before any implementation.

- [ ] **Automated tests - long-term: broad program coverage** - TagFixer tests (done) and routing tests (Tier 1) deliver the foundation first. This entry covers ongoing expansion once routing tests are stable. Expand only when a real bug escapes current test coverage - never speculatively.
  - **Motivation (unchanged):** Each fix session currently requires 2-3 manual dry run + force regen cycles. Every module covered by a test eliminates that cycle for that module. Real integration (May 2026) found metadata edge cases dry-run missed - tests for the same logic would have caught several earlier.
  - **Infrastructure in place:** Inline `--test` flag, 20-line Assert class, test.bat, launch.bat integration. DIY - no xUnit, no separate project. Old-style csproj manual registration + no VS test runner in the build workflow makes a framework overkill.
  - **Expansion rule:** Add a test when a real bug escapes current coverage. Not before.
  - **Current coverage (comprehensive as of 2026-05-31, all tests green):** TagFixer pure functions and patterns, routing (fixture-based, all Artists/Misc/Musivation/Motivation paths), ParseCache (round-trip and corruption), LibChecker (all rules: album subfolder, genre-vs-folder, compilation, covers, duplicates, mismatch, loose file, Sources OST), Track.ProcessProperty, StatList frequency distribution.
  - **Remaining expansion candidates (in value order):**
    - **ATD routing test isolation** - `CountAkiraTheDonPersonSongs` uses `Constants.AudioFolderPath` hardcoded (not `_libraryPath`), so the "below threshold" and "People/{person}/" branches cannot be unit tested without scanning the real library. Fix: add `string libraryPath = null` parameter to CountAkiraTheDonPersonSongs and CountAkiraTheDonPersonAlbumSongs, defaulting to `Constants.AudioFolderPath`. Tests can then pass the fixture path. Same pattern as the existing test constructor for MusicIntegrator.
    - TagFixer full pipeline: real .mp3 test fixture -> run TagFixer -> assert ID3 tags changed correctly. Covers the file I/O layer that pure-function tests don't touch.
    - Reflector: snapshot test - known track -> verify XML output format stays stable across refactors.
    - AudioMirrorCommitter: verify commit is skipped when LibChecker has hits; verify it runs on force regen + clean run.
  - **Scope discipline:** Test feature behavior, not individual function internals. "Artist casing is preserved end-to-end" not "ExtractAndFixArtists() branch 47". Internals are tested indirectly; changing internals should not break tests if behavior is unchanged.

- [ ] **Compilation album track routing** - DECISION NEEDED - run /think on: "Where should compilation album tracks route when multiple primary artists exist?" User preference framing: tracks where the primary artist has an existing folder -> their `Singles/`; tracks where no artist folder exists -> `Compilations/<album>/` (e.g. `C:\Users\David\Audio\Compilations\`); minimize Misc; avoid single-track folders. Detection signal: compilation = multiple distinct primary artists across tracks in the same album. Applies to cases like 'Barbie The Album'.

- [ ] **Album art dimensions - Phase 2 (Analyse) + Phase 3 (Enforce)** - Phase 1 (Capture) done 2026-05-31: `<CoverWidth>` and `<CoverHeight>` now written to every AudioMirror XML on force regen. Next phases:
  - **(2) Analyse** - scan XMLs after a force regen to produce a dimension distribution report: histogram of common dimension pairs (500x500, 600x600, 300x300, etc.), count of tracks with no cover (0/null dimensions), count of non-square covers (W != H). Let the data determine the minimum quality target before writing any rule.
  - **(3) Enforce** - once threshold is agreed, add LibChecker rules with tiered severity: (a) **ERROR: no cover** (0/null dimensions - always wrong); (b) **ERROR: non-square cover** (W != H - album art must always be square); (c) **WARNING: below threshold** (e.g., < 500x500 is streaming standard; 300x300 acceptable for older rips). Threshold must be configurable, not hardcoded - library has legitimate old rips. Rule (a) and (b) are unconditional; only (c) needs a threshold decision.

---

## TIER 3 - POLISH

**Goal: close structural gaps and improve developer experience. Non-blocking.**

- [ ] **[INVESTIGATE] Reflector incremental: refresh XMLs when MP3 is newer than its XML** - Reflector's `CreateFile` only runs `if (!File.Exists(...))` - it skips any XML that already exists, even if the underlying MP3 has been edited since the XML was last written. Result: tag edits made in Mp3tag (or any external tool) don't appear in incremental analysis until the next force regen. Investigation: compare `MP3.LastWriteTime` vs `XML.LastWriteTime` per file; if MP3 is newer, delete the XML so Reflector recreates it (or re-parse directly). Key question before implementing: how often do tags actually change post-integration? If the answer is "rarely" (the library is stable after integration), the current workaround (force regen) may be sufficient and the fix adds complexity for little gain. Check actual frequency before deciding. See DevContext.md for full architecture context.

- [ ] **[INVESTIGATE] Reflector incremental: clean up orphaned XMLs for deleted MP3s** - When an MP3 is deleted from the library, its corresponding XML in AudioMirror is never removed by incremental Reflector (it only iterates real files and creates). The orphaned XML persists across all subsequent incremental analyses - the deleted song appears as a ghost track in reports, LibChecker, and ParseCache until the next force regen. Investigation: after building the real-file list, scan the mirror for XMLs with no corresponding real file and remove them. Key question: how often do library deletions happen? If deletions are always followed by force regen (expected workflow), the orphaned-XML cleanup is already handled. Confirm actual deletion workflow before implementing. See DevContext.md.

- [ ] **Comprehensive library audit: validate conformance, analyze patterns, unify rules** - Three tightly-coupled goals: (1) AUDIT: Scan AudioMirror XMLs against Music-Library-Rules.md; produce violations/gaps report. Confirm LibChecker catches all mandated rules. Also scan for systematic metadata brittleness surfaced by Stage 3C findings: casing inconsistencies, character illegality (special chars in filenames), and metadata-vs-folder mismatches - find edge cases BEFORE next batch integration; complements character validation by revealing existing library state. Implementation notes: FEEDBACK-Stage3C.md lines 342-345. *Partial progress (2026-04-09): rules gap analysis done, CheckAlbumSubfolderRule() + CheckGenreVsFolder() added. Remaining: run full library audit, identify gaps.* (2) ANALYSE: Extract patterns from decision XMLs + AudioMirror data (artist folder distribution, album patterns, genre consistency, file counts). Build statistical models to identify high-confidence auto-routing cases. Payback: reduces manual confirmations beyond rule-based routing. (3) UNIFY: Refactor routing rules and LibChecker validation into single `RulesEngine` - rules defined once, consumed by both MusicIntegrator and LibChecker. Prevents sync failures (current: TagFixer SKIPPED but LibChecker FLAGGED because rules diverged). All three feed each other: audit finds gaps -> patterns suggest improvements -> unified rules prevent future divergence. Blocked by: auto-routing stable first (so patterns are meaningful).
  - **Think pass first:** Before any implementation, run `/think` on the audit design. Key questions: what violation types are first-class vs derived, what output format serves both human review and future re-runs, and whether audit logic belongs in the main program or stays external. The think pass resolves this before code is written.
  - **Integration consideration:** The audit script may be worth absorbing into AudioManager itself as a `--audit` mode (alongside `--dry-run`, `--integrate`, etc.). Arguments for integration: (a) audit logic needs the same XML-parsing and rules knowledge already in the codebase - duplication risk if kept external; (b) a built-in mode can be run on demand at any time with no separate toolchain; (c) output can share the existing run log format. Arguments against: audit is infrequent and exploratory - a standalone script is lower risk to implement and easier to iterate. Decide during the think pass; if integrated, it becomes a proper `--audit` CLI flag wired into `launch.bat`.
  - **Script approach (token-saving):** Whether standalone or integrated, the audit produces a structured violations report (count, severity, representative examples per violation type) that Claude reviews as output - not by scanning raw XMLs file-by-file. Dramatically reduces token load and makes the audit repeatable without AI involvement.

---

## TIER 4 - FUTURE

**Goal: exploratory features and advanced enhancements, tackled after core tiers are stable.**

- **Refactor CountAkiraTheDonPersonSongs/AlbumSongs - artist as parameter** - [code smell] `CountAkiraTheDonPersonSongs(sampledPerson)` and `CountAkiraTheDonPersonAlbumSongs(sampledPerson, album)` hardcode the artist ("Akira The Don") in the function name. These should become `CountPersonSongs(artist, sampledPerson)` and `CountPersonAlbumSongs(artist, sampledPerson, album)` so the same logic can serve future artists with the same People/ folder structure. Only matters if a second artist with a People/-style structure is added.

- **Sources/ routing not implemented in GetDestDir()** - `Constants.SourcesDir` exists but `MusicIntegrator.GetDestDir()` has no routing logic for Sources/Films, Sources/Shows, or Sources/Anime. Films/Shows/Anime tracks currently fall to Misc and require manual folder-picker redirection. `Music-Library-Rules.md` documents the expected routing rules (Films subfolder = film name, Shows subfolder = show name, Anime = separate).
  - **Challenge:** Automating this is difficult - metadata alone rarely indicates source type. Current approach: if `Album` contains "OST" or "Soundtrack", prompt user for subfolder choice rather than defaulting to Misc.
  - **Exploratory:** Study existing Sources/Films, Sources/Shows, Sources/Anime tracks for metadata patterns (album names, artist names, genre, etc.) that distinguish them. May reveal rough heuristics for auto-detection, or may confirm that manual folder-picker prompt is the only reliable solution.

- **Audit libchecker-exceptions.xml - distinguish bug fixes from genuine exceptions** - Review all exceptions to identify which are workarounds for LibChecker regex bugs (e.g., "version" in legitimate titles like Alan Watts tracks) vs. genuine tracks that need exemption (e.g., Eric Thomas bonus content). For each regex-bug workaround, ensure there's a corresponding TIER 1 code improvement in IDEAS. Goal: exceptions.xml contains only track-specific needs, not rule improvements.

- **Evaluate removing .md integration logs in favour of decision XMLs** - Integration logs (`logs/integration-*.md`) and decision XMLs overlap in content. If XMLs contain all routing information, the .md logs may be redundant. Evaluate what .md logs have that XMLs don't (human-readable narrative vs structured data). If XMLs are sufficient for audit and analysis, remove .md logs to simplify the output.

- **Routing decision analysis mode** - Add a mode that reads decision XMLs, cross-references routing decisions against routing rules code and LibChecker rules, and flags inconsistencies. Produces a report: "these N files were routed to X but LibChecker would flag them as Y". Pairs well with the "Centralise rules" refactor. Exploratory - assess value after the first real integration run produces decision XML data to analyse.

- **"My Edits" tracking** - detect locally edited songs by comparing duration to official track (>3-4s diff = protected from overwrite).
- **Parody/original song pairing detection** - flag songs where a parody and its original are both in the library.
- **Album completion detection** - cross-reference library against Spotify/MusicBrainz; flag where 50%+ of an album is owned.
- **Fuzzy artist name matching** - handle artist name variations during routing ("The Beatles" vs "Beatles", featured artist formatting differences). Only matters at scale.
- **Fuzzy duplicate title matching** - extend the pre-integration duplicate check to catch near-matches (e.g. "Song (feat. X)" vs "Song", "Song - Remix" vs "Song"). Approach: normalise both sides before comparison by stripping featured artist parentheticals, stripping remix/edit/version suffixes, collapsing whitespace. Blocked by: the exact-match duplicate check must be in place first.
- **Neural network routing (exploratory)** - Train a simple neural network on AudioMirror library commit history and routing decisions to learn implicit routing patterns instead of defining everything statically. Model input: track metadata (artist, album, tags, file structure). Model output: routing destination. Payback: reduces boilerplate routing rules, evolves with library patterns. Very low priority, exploratory phase only - assess whether domain patterns are learnable and whether ML overhead justifies the benefit.

---

## See Also

- `docs/Development/HISTORY.md` - completed features, settled design decisions, parked ideas
- `docs/Development/GUI-ROADMAP.md` - GUI planning: webapp, tabs, Sonarr/Radarr vision, far-future integrations
- `docs/References/Music-Library-Rules.md` - canonical rules for library structure
- `docs/Historical/NewMusic-Integration-Plan-20260308.md` - past batch integration (March 2026 batch A)
- `docs/Historical/NewMusic-Integration-Plan-20260407.md` - past batch integration (April 2026)
- `docs/References/AudioMirror-Format.md` - AudioMirror XML format and repo info

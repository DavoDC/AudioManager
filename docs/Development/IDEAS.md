# Ideas & Future Work

Single source of truth for all pending work. **CLI only.** GUI planning: `GUI-ROADMAP.md`. Completed items -> `HISTORY.md`.

Items are tiered by priority. Do not advance to the next tier until the current tier is verified on real data.

---

## TIER 1 - BLOCKING

**Goal: deliver auto-routing for known cases - eliminate confirmation fatigue. Prerequisites must be verified on real data first.**

**TIER 1 threshold:** anything that would cause a LibChecker warning belongs here, regardless of where or when it was discovered. Routing gaps, rule divergence, config omissions - all TIER 1 if LibChecker would fire on it.

**ACTIVE FOCUS:** Routing tests (GetDestDir) are the top Tier 1 item - start here. Integration bugs below are code-ready; verify at next integration batch. Tests pay back every subsequent fix session.

---

## TIER 2 - QUALITY

**Goal: improve UX, add test coverage, and audit metadata quality.**




- [ ] **Automated tests - long-term: broad program coverage** - TagFixer tests (done) and routing tests (Tier 1) deliver the foundation first. This entry covers ongoing expansion once routing tests are stable. Expand only when a real bug escapes current test coverage - never speculatively.
  - **Motivation (unchanged):** Each fix session currently requires 2-3 manual dry run + force regen cycles. Every module covered by a test eliminates that cycle for that module. Real integration (May 2026) found metadata edge cases dry-run missed - tests for the same logic would have caught several earlier.
  - **Infrastructure in place:** Inline `--test` flag, 20-line Assert class, test.bat, launch.bat integration. DIY - no xUnit, no separate project. Old-style csproj manual registration + no VS test runner in the build workflow makes a framework overkill.
  - **Expansion rule:** Add a test when a real bug escapes current coverage. Not before. Signal: if total test count exceeds 50 and no bug has escaped since last expansion, the suite is adequate for current session cadence.
  - **Expansion candidates (in value order, after routing tests are stable):**
    - LibChecker rules: one test per rule (CheckAlbumSubfolderRule, CheckGenreVsFolder, etc.) - verify each rule fires on known-bad input and stays silent on clean input. Especially valuable for the "version/bonus" regex item in TIER 1.
    - TagFixer full pipeline: real .mp3 test fixture -> run TagFixer -> assert ID3 tags changed correctly. Covers the file I/O layer that the existing pure-function tests don't touch.
    - Reflector: snapshot test - known track -> verify XML output format stays stable across refactors.
    - Analyser: known library fixture -> verify report numbers are correct (track counts, genre distribution).
    - AudioMirrorCommitter: verify commit is skipped when LibChecker has hits; verify it runs on force regen + clean run.
  - **Scope discipline:** Test feature behavior, not individual function internals. "Artist casing is preserved end-to-end" not "ExtractAndFixArtists() branch 47". Internals are tested indirectly; changing internals should not break tests if behavior is unchanged.



- [ ] **Compilation album track routing** - DECISION NEEDED - run /think on: "Where should compilation album tracks route when multiple primary artists exist?" User preference framing: tracks where the primary artist has an existing folder -> their `Singles/`; tracks where no artist folder exists -> `Compilations/<album>/` (e.g. `C:\Users\David\Audio\Compilations\`); minimize Misc; avoid single-track folders. Detection signal: compilation = multiple distinct primary artists across tracks in the same album. Applies to cases like 'Barbie The Album'.

- [ ] **Album art dimensions audit and enforcement** - Library has no enforced minimum for embedded album art size. Three-phase approach: (1) **Capture** - extend Reflector to write `<CoverWidth>` and `<CoverHeight>` fields into AudioMirror XMLs (TagLib# exposes `tag.Pictures[0]` with width/height); (2) **Analyse** - after a full regen, scan XMLs to produce a distribution of cover dimensions across the library (histogram: how many tracks at 500x500, 600x600, 300x300, etc.) - let the data reveal where the majority sits before picking a target; (3) **Enforce** - once a sensible minimum is agreed (e.g. 500x500), add a LibChecker rule flagging tracks below that threshold, and optionally warn in TagFixer during integration if incoming tracks have undersized art. Do not guess a limit before running the analysis.

---

## TIER 3 - POLISH

**Goal: close structural gaps and improve developer experience. Non-blocking.**



- [ ] **AudioMirrorCommitter: re-enable auto-commit with specific trigger conditions** - Auto-commit AudioMirror (`C:\Users\David\GitHubRepos\AudioMirror`) when LibChecker is clean. Specific trigger conditions: COMMIT on analysis (force regen) OR integration. DO NOT commit on analysis (non-force regen) - incremental mirror may have stale XMLs, only force regen and integration produce a fully reliable state. Commit format must match existing AudioMirror git log conventions (check `git log` in AudioMirror repo for format). Implementation: uncomment logic in `AudioMirrorCommitter.TryCommit()` (lines ~60-85), add trigger-mode parameter so caller can specify which mode triggered the run. Safety gate: if Reflector found unexpected extensions OR LibChecker reported any hits, skip commit and notify user - auto-commit only when entire pipeline is completely clean. Blocked by: TIER 1 bugs fixed first (stale XMLs from Misc Migration and library replacements must be resolved so LibChecker is clean after integration, otherwise auto-commit never fires).

- [ ] **Comprehensive library audit: validate conformance, analyze patterns, unify rules** - Three tightly-coupled goals: (1) AUDIT: Scan AudioMirror XMLs against Music-Library-Rules.md; produce violations/gaps report. Confirm LibChecker catches all mandated rules. Also scan for systematic metadata brittleness surfaced by Stage 3C findings: casing inconsistencies, character illegality (special chars in filenames), and metadata-vs-folder mismatches - find edge cases BEFORE next batch integration; complements character validation by revealing existing library state. Implementation notes: FEEDBACK-Stage3C.md lines 342-345. *Partial progress (2026-04-09): rules gap analysis done, CheckAlbumSubfolderRule() + CheckGenreVsFolder() added. Remaining: run full library audit, identify gaps.* (2) ANALYSE: Extract patterns from decision XMLs + AudioMirror data (artist folder distribution, album patterns, genre consistency, file counts). Build statistical models to identify high-confidence auto-routing cases. Payback: reduces manual confirmations beyond rule-based routing. (3) UNIFY: Refactor routing rules and LibChecker validation into single `RulesEngine` - rules defined once, consumed by both MusicIntegrator and LibChecker. Prevents sync failures (current: TagFixer SKIPPED but LibChecker FLAGGED because rules diverged). All three feed each other: audit finds gaps -> patterns suggest improvements -> unified rules prevent future divergence. Blocked by: auto-routing stable first (so patterns are meaningful).
  - **Think pass first:** Before any implementation, run `/think` on the audit design. Key questions: what violation types are first-class vs derived, what output format serves both human review and future re-runs, and whether audit logic belongs in the main program or stays external. The think pass resolves this before code is written.
  - **Integration consideration:** The audit script may be worth absorbing into AudioManager itself as a `--audit` mode (alongside `--dry-run`, `--integrate`, etc.). Arguments for integration: (a) audit logic needs the same XML-parsing and rules knowledge already in the codebase - duplication risk if kept external; (b) a built-in mode can be run on demand at any time with no separate toolchain; (c) output can share the existing run log format. Arguments against: audit is infrequent and exploratory - a standalone script is lower risk to implement and easier to iterate. Decide during the think pass; if integrated, it becomes a proper `--audit` CLI flag wired into `launch.bat`.
  - **Script approach (token-saving):** Whether standalone or integrated, the audit produces a structured violations report (count, severity, representative examples per violation type) that Claude reviews as output - not by scanning raw XMLs file-by-file. Dramatically reduces token load and makes the audit repeatable without AI involvement.

- [ ] **Dupe routing UX clarity** - When the duplicate resolver shows `[L] Delete library copy`, it is not obvious that the kept file will be routed/placed during the integration step that follows. Add a one-line note after the dupe resolution prompt: "The kept file will be routed in the next step." Eliminates confusion about where the track ends up.

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

- **Leverage library structure to speed up parsing** - Parser currently scans every XML file on every run regardless of what changed. The library has a predictable folder structure (Artists/{artist}/{album}/, Musivation/, Misc/) - could use this to skip unchanged subtrees. Approach: (a) compare folder mtimes against `LastRunInfo.txt` timestamp to identify unchanged artist dirs and skip re-parsing their XMLs; (b) cache parsed tags per folder keyed on folder mtime; (c) only force full re-parse on `Recreated: True` (force regen). Payback scales with library size - already noticeable at current size, significant once library doubles. Requires benchmarking parse time vs filesystem stat calls to confirm the tradeoff is positive.

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

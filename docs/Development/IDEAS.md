# Ideas & Future Work

Single source of truth for all pending work. **CLI only.** GUI planning: `GUI-ROADMAP.md`. Completed items -> `HISTORY.md`.

Items are tiered by priority. Do not advance to the next tier until the current tier is verified on real data.

---

## TIER 1 - BLOCKING

**Goal: deliver auto-routing for known cases - eliminate confirmation fatigue. Prerequisites must be verified on real data first.**

**TIER 1 threshold:** anything that would cause a LibChecker warning belongs here, regardless of where or when it was discovered. Routing gaps, rule divergence, config omissions - all TIER 1 if LibChecker would fire on it.

**RETROSPECTIVES (COMPLETED - see HISTORY.md for details)**

- [ ] **TMRWNITE artist casing investigation** - Dry-run 2026-05-26 shows Path as `Artists\Tmrwnite\...` but scan-ahead shows `Artists/TMRWNITE/`. TagFixer.ExtractAndFixArtists may be title-casing all-caps names. If so, `CheckArtistFolder()` (case-sensitive String.Equals) will flag on next analysis. Before integrating this batch: (1) run analysis after dry-run to confirm whether TMRWNITE is in artist-name-overrides.xml, (2) if not, add it, (3) trace whether ExtractAndFixArtists applies any casing normalisation to all-caps names and fix if so.

---

## TIER 2 - QUALITY

**Goal: improve UX, add test coverage, and audit metadata quality.**

- [ ] **Compilation album track routing** - DECISION NEEDED - run /think on: "Where should compilation album tracks route when multiple primary artists exist?" User preference framing: tracks where the primary artist has an existing folder -> their `Singles/`; tracks where no artist folder exists -> `Compilations/<album>/` (e.g. `C:\Users\David\Audio\Compilations\`); minimize Misc; avoid single-track folders. Detection signal: compilation = multiple distinct primary artists across tracks in the same album. Applies to cases like 'Barbie The Album'.

- [ ] **Add minimal automated tests for three broad features** - TDD approach for three core features only. ROI analysis showed full test suite expansion not worth it; strict scope discipline prevents token waste.
  - **Motivation:** Each session requires multiple manual dry runs and force regens to verify fixes. Tests for key features catch regressions immediately at build time, enabling faster iteration. Current feedback: each fix session needs 2-3 manual verification cycles (dry run, force regen, spot-check). Tests eliminate this.
  - **Testing strategy:** Write tests FIRST (TDD), implement features to pass them. Once all three features have green tests, STOP - do not expand beyond these three unless a regression is found that tests didn't catch. Rule: expand test suite only when real bugs escape testing, not speculatively.
  - **Feature 1: Build and launch** - Program compiles cleanly via MSBuild and runs without crashing (e.g. `--help` works). Catches compilation/linkage regressions immediately. Payback: weeks.
  - **Feature 2: Tag normalization** - TagFixer correctly cleans and normalizes tags: TCMP flags set, artist casing preserved (mike. stays lowercase), genre set per rules (Musivation genre for Musivation artists), suffixes stripped (album field cleaned). Payback: 2-3 weeks (catches tag mutation regressions that fail in LibChecker days later).
  - **Feature 3: Routing correctness** - GetDestDir() produces correct destination paths given artist, album, and library state. Sample cases: 3+ songs from album -> album subfolder, 1-2 songs -> Singles, known artist folders -> existing folders, Musivation artists -> Musivation folder. Payback: 1-2 months (wrong routing = real files moved wrong - highest risk).
  - **Scope:** Focus on feature behavior, not individual function testing. Test "artist casing is preserved" not "ExtractAndFixArtists() line 47". Test "Musivation artist gets Musivation folder" not "GetDestDir() branch coverage".
  - **Skipped:** full LibChecker validation (add rules incrementally), full integration test (dry-run covers this), edge case enumeration, speculative test coverage beyond the three features.
  - Create `AudioManager.Tests` xUnit project; wire into `launch.bat` as "Run tests" menu item; broken tests block exe launch.

- [ ] **Machine-readable dry-run output mode (`--json-output`)** - Add a flag that writes dry-run results as structured JSON to stdout (or a file) instead of human-readable text. Enables: (a) Claude to parse routing decisions programmatically for automated assertions, (b) future test harness to diff expected vs actual routing without string parsing. Schema: array of routing decisions per file - `{filename, artist, title, album, destination, reason, confidence, tagChanges[], inBatchDuplicate}`. Duplicates and Misc migrations also included. Standard text output stays unchanged for human use; JSON mode is additive. Payback: makes Claude's dry-run verification more precise (flag exact routing regressions, not just visual inspection) and paves the way for automated regression tests once test infrastructure exists.

- [ ] **Album art dimensions audit and enforcement** - Library has no enforced minimum for embedded album art size. Three-phase approach: (1) **Capture** - extend Reflector to write `<CoverWidth>` and `<CoverHeight>` fields into AudioMirror XMLs (TagLib# exposes `tag.Pictures[0]` with width/height); (2) **Analyse** - after a full regen, scan XMLs to produce a distribution of cover dimensions across the library (histogram: how many tracks at 500x500, 600x600, 300x300, etc.) - let the data reveal where the majority sits before picking a target; (3) **Enforce** - once a sensible minimum is agreed (e.g. 500x500), add a LibChecker rule flagging tracks below that threshold, and optionally warn in TagFixer during integration if incoming tracks have undersized art. Do not guess a limit before running the analysis.

---

## TIER 3 - POLISH

**Goal: close structural gaps and improve developer experience. Non-blocking.**

- [ ] **Album tag inheritance from folder name for incomplete tags** - If album tag is empty on a file from a subfolder, all tracks route to Singles regardless of batch count. Fix: in MusicIntegrator routing, inherit album name from the folder name as a fallback when album tag is missing. Not blocking. (Part (a) - filename guard for empty artist/title - done 2026-05-26.)

- [ ] **Remove RoutingConfidence enum - replace with `bool isNewFolder`** - [code smell] The enum had three values: `Certain`, `Likely`, `Uncertain`. `Uncertain` is now dead code (Misc routes as Certain since 2026-05-25). `Likely` means "scan-ahead is creating a new Artists/ folder" - not a confidence level, a structural note. The enum was designed for a world where some routes prompted the user; that world no longer exists. **Fix:** Replace the enum with `out bool isNewFolder` in `GetDestDir()`. Display: `[AUTO]` vs `[AUTO - new folder]` (more accurate than `[AUTO (likely)]`). **Broader principle this surfaces:** the `RoutingConfidence` abstraction assumed uncertainty gets resolved at integration time. It should be resolved in the dry-run review loop instead. The correct contract is: iterate dry runs until routing looks right, then integrate - integration always auto-routes, no prompts. The code will fully embody this once the enum is removed.

- [ ] **AudioMirrorCommitter: safety gates then re-enable auto-commit** - Two phases: (1) SAFETY GATES: currently skips auto-commit only if LibChecker reported hits, but doesn't account for other issues like unexpected file extensions. Update check: if Reflector found unexpected extensions OR LibChecker reported hits, skip commit and notify user. Goal: auto-commit only runs when entire pipeline is completely clean. (2) RE-ENABLE: once safety gates proven stable (several weeks of runs, zero accidental data loss), re-enable auto-commit by uncommenting logic in `AudioMirrorCommitter.TryCommit()` (lines ~60-85). Blocked by: TIER 1 all verified first. Note: manual commits are safe, re-enable is nice-to-have, not critical.

- [ ] **Comprehensive library audit: validate conformance, analyze patterns, unify rules** - Three tightly-coupled goals: (1) AUDIT: Scan AudioMirror XMLs against Music-Library-Rules.md; produce violations/gaps report. Confirm LibChecker catches all mandated rules. Also scan for systematic metadata brittleness surfaced by Stage 3C findings: casing inconsistencies, character illegality (special chars in filenames), and metadata-vs-folder mismatches - find edge cases BEFORE next batch integration; complements character validation by revealing existing library state. Implementation notes: FEEDBACK-Stage3C.md lines 342-345. *Partial progress (2026-04-09): rules gap analysis done, CheckAlbumSubfolderRule() + CheckGenreVsFolder() added. Remaining: run full library audit, identify gaps.* (2) ANALYSE: Extract patterns from decision XMLs + AudioMirror data (artist folder distribution, album patterns, genre consistency, file counts). Build statistical models to identify high-confidence auto-routing cases. Payback: reduces manual confirmations beyond rule-based routing. (3) UNIFY: Refactor routing rules and LibChecker validation into single `RulesEngine` - rules defined once, consumed by both MusicIntegrator and LibChecker. Prevents sync failures (current: TagFixer SKIPPED but LibChecker FLAGGED because rules diverged). All three feed each other: audit finds gaps -> patterns suggest improvements -> unified rules prevent future divergence. Blocked by: auto-routing stable first (so patterns are meaningful).
  - **Think pass first:** Before any implementation, run `/think` on the audit design. Key questions: what violation types are first-class vs derived, what output format serves both human review and future re-runs, and whether audit logic belongs in the main program or stays external. The think pass resolves this before code is written.
  - **Integration consideration:** The audit script may be worth absorbing into AudioManager itself as a `--audit` mode (alongside `--dry-run`, `--integrate`, etc.). Arguments for integration: (a) audit logic needs the same XML-parsing and rules knowledge already in the codebase - duplication risk if kept external; (b) a built-in mode can be run on demand at any time with no separate toolchain; (c) output can share the existing run log format. Arguments against: audit is infrequent and exploratory - a standalone script is lower risk to implement and easier to iterate. Decide during the think pass; if integrated, it becomes a proper `--audit` CLI flag wired into `launch.bat`.
  - **Script approach (token-saving):** Whether standalone or integrated, the audit produces a structured violations report (count, severity, representative examples per violation type) that Claude reviews as output - not by scanning raw XMLs file-by-file. Dramatically reduces token load and makes the audit repeatable without AI involvement.

- [ ] **Simplify launch.bat - move menu logic into Program.cs** - RivalsVidMaker's `run.bat` is 2 lines; all menu/mode logic lives in Python. AudioManager's `launch.bat` is 91 lines with its own menu, duplicating CLI arg logic already in `Program.cs`. Refactor: `launch.bat` becomes a ~5-line wrapper that just builds + runs `AudioManager.exe` with no args; all menu logic lives in Program.cs where it's testable and debuggable.

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

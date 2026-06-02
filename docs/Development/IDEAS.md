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

- [ ] **Automated tests - long-term: broad program coverage** - TagFixer tests (done) and routing tests (Tier 1) deliver the foundation first. This entry covers ongoing expansion once routing tests are stable. Expand only when a real bug escapes current test coverage - never speculatively.
  - **Motivation (unchanged):** Each fix session currently requires 2-3 manual dry run + force regen cycles. Every module covered by a test eliminates that cycle for that module. Real integration (May 2026) found metadata edge cases dry-run missed - tests for the same logic would have caught several earlier.
  - **Infrastructure in place:** Inline `--test` flag, 20-line Assert class, test.bat, launch.bat integration. DIY - no xUnit, no separate project. Old-style csproj manual registration + no VS test runner in the build workflow makes a framework overkill.
  - **Expansion rule:** Add a test when a real bug escapes current coverage. Not before.
  - **Current coverage (comprehensive as of 2026-06-01, all tests green):** TagFixer (pure functions + null guards), routing (all paths including ATD 4 paths, scan-ahead, album thresholds), ParseCache (round-trip, staleness, corruption), LibChecker (all rules, dirty + clean baselines), Track.ProcessProperty, StatList (GetSortedFreqDist + GetDecadeFreqDist), TrackXML (round-trip + special chars + missing element), Parser (reads XMLs, skips README, throws on non-XML, cache hit), Reflector.SanitiseFilename (all invalid char behaviors + case preservation), AgeChecker (all 5 branches), TeeWriter (capture writer, file output, per-line timestamps, embedded newline invariant).
  - **Remaining expansion candidates (in value order):**
    - TagFixer full pipeline: real .mp3 test fixture -> run TagFixer -> assert ID3 tags changed correctly. Covers the file I/O layer that pure-function tests don't touch. Requires either a real MP3 file committed to the repo or a synthesized minimal MP3 byte sequence.
    - AudioMirrorCommitter: the gating logic (skip on incremental, skip on dirty) has no return value so tests require either a refactor to return a status enum, or injecting a spy. The git operations require a temp git repo.
    - LibChecker exceptions mechanism: **done** - `LibCheckerExceptionTests.cs` covers wildcard, specific-match, and non-match cases (3 tests, all passing as of 2026-06-01).
  - **Scope discipline:** Test feature behavior, not individual function internals. "Artist casing is preserved end-to-end" not "ExtractAndFixArtists() branch 47". Internals are tested indirectly; changing internals should not break tests if behavior is unchanged.

- [ ] **Album art dimensions - Phase 3 (Enforce)** - Phases 1 (Capture 2026-05-31) and 2 (Analyse 2026-06-02) done. Cover fields grouped under `<AlbumCover>` (force regen required to migrate). Analyser already prints coverage, non-square count, and dimension histogram. **Next:** after running force regen and reviewing the dimension report, add LibChecker rules with tiered severity: (a) **ERROR: no cover** (Width=0 - always wrong); (b) **ERROR: non-square cover** (W != H); (c) **WARNING: below threshold** (e.g., < 500x500; configurable, not hardcoded - library has legitimate old rips). Rules (a) and (b) unconditional; only (c) needs threshold decision based on the data.

---

## TIER 3 - POLISH

**Goal: close structural gaps and improve developer experience. Non-blocking.**




---

## TIER 4 - FUTURE

**Goal: exploratory features and advanced enhancements, tackled after core tiers are stable.**

- **Pattern analysis: extract routing patterns from decision XMLs + AudioMirror data** - Artist folder distribution, album patterns, genre consistency. Build statistical models to identify high-confidence auto-routing cases. Blocked by: need real integration decision XML data from multiple runs.

- **Rules unification: single RulesEngine for MusicIntegrator + LibChecker** - Rules currently defined separately in GetDestDir and LibChecker; divergence has caused "TagFixer SKIPPED but LibChecker FLAGGED" bugs. Refactor into a shared RulesEngine consumed by both. Prerequisites: routing stable, patterns understood.

- **Refactor CountAkiraTheDonPersonSongs/AlbumSongs - artist as parameter** - [code smell] `CountAkiraTheDonPersonSongs(sampledPerson)` and `CountAkiraTheDonPersonAlbumSongs(sampledPerson, album)` hardcode the artist ("Akira The Don") in the function name. These should become `CountPersonSongs(artist, sampledPerson)` and `CountPersonAlbumSongs(artist, sampledPerson, album)` so the same logic can serve future artists with the same People/ folder structure. Only matters if a second artist with a People/-style structure is added.

- **Sources/ routing not implemented in GetDestDir()** - `Constants.SourcesDir` exists but `MusicIntegrator.GetDestDir()` has no routing logic for Sources/Films, Sources/Shows, or Sources/Anime. Films/Shows/Anime tracks currently fall to Misc and require manual folder-picker redirection. `Music-Library-Rules.md` documents the expected routing rules (Films subfolder = film name, Shows subfolder = show name, Anime = separate).
  - **Challenge:** Automating this is difficult - metadata alone rarely indicates source type. Current approach: if `Album` contains "OST" or "Soundtrack", prompt user for subfolder choice rather than defaulting to Misc.
  - **Exploratory:** Study existing Sources/Films, Sources/Shows, Sources/Anime tracks for metadata patterns (album names, artist names, genre, etc.) that distinguish them. May reveal rough heuristics for auto-detection, or may confirm that manual folder-picker prompt is the only reliable solution.


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

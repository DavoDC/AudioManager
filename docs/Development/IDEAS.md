# Ideas & Future Work

Single source of truth for all pending work. **CLI only.** GUI planning: `GUI-ROADMAP.md`. Completed items -> `HISTORY.md`.

Items are tiered by priority. Do not advance to the next tier until the current tier is verified on real data.

---

## TIER 1 - BLOCKING

**Goal: deliver auto-routing for known cases - eliminate confirmation fatigue. Prerequisites must be verified on real data first.**

**TIER 1 threshold:** anything that would cause a LibChecker warning belongs here, regardless of where or when it was discovered. Routing gaps, rule divergence, config omissions - all TIER 1 if LibChecker would fire on it.

**ACTIVE FOCUS:** Routing test suite complete (all tests green as of 2026-05-31). LibChecker.CheckCompilationsFolder tightened 2026-06-03 (genuine various-artist compilations now exempt). Remaining TIER 1 items require verification on next real integration batch.

- [ ] **[BLOCKED: do after Raphael trip] Fix MP3 filename casing to match artist ID3 tags** - several files have filenames with old/inconsistent artist casing that diverges from the ID3 tag (e.g., `Bowling For Soup - 1985.mp3` but tag says `Bowling for Soup`; `24kgoldnDaBaby - Coco.mp3` but tag says `24kGoldn`; similarly `Iann Dior` vs `iann dior`, `JAY-Z` vs `Jay-Z`, `Kota the Friend` vs `KOTA The Friend`). Fix via mp3tag on the master machine - rename files to match the ID3 artist tag casing. This WILL fix all current LibChecker casing warnings (genuine mismatches, not false positives). **Must be done before next integration so LibChecker is clean. Raphael has master copy of audio library - do not edit on e15 to avoid sync conflict.**

---

## TIER 2 - QUALITY

**Goal: improve UX, add test coverage, and audit metadata quality.**

- [ ] **Automated tests - long-term: broad program coverage** - TagFixer tests (done) and routing tests (Tier 1) deliver the foundation first. This entry covers ongoing expansion once routing tests are stable. Expand only when a real bug escapes current test coverage - never speculatively.
  - **Motivation (unchanged):** Each fix session currently requires 2-3 manual dry run + force regen cycles. Every module covered by a test eliminates that cycle for that module. Real integration (May 2026) found metadata edge cases dry-run missed - tests for the same logic would have caught several earlier.
  - **Infrastructure in place:** Inline `--test` flag, 20-line Assert class, test.bat, launch.bat integration. DIY - no xUnit, no separate project. Old-style csproj manual registration + no VS test runner in the build workflow makes a framework overkill.
  - **Expansion rule:** Add a test when a real bug escapes current coverage. Not before.
  - **Current coverage (comprehensive as of 2026-06-02, 161 tests green):** TagFixer (pure functions + null guards), routing (all paths including ATD 4 paths, scan-ahead, album thresholds, compilation albums), ParseCache (round-trip, staleness, corruption), LibChecker (all rules including Compilations/ validation), Track.ProcessProperty, StatList (GetSortedFreqDist + GetDecadeFreqDist), TrackXML (round-trip + special chars + missing element), Parser (reads XMLs, skips README, throws on non-XML, cache hit), Reflector (SanitiseFilename all invalid char behaviors + case preservation, IsStaleMirrorXml stale detection), AgeChecker (all 5 branches), TeeWriter (capture writer, file output, per-line timestamps, embedded newline invariant).
  - **Remaining expansion candidates (in value order):**
    - TagFixer full pipeline: real .mp3 test fixture -> run TagFixer -> assert ID3 tags changed correctly. Covers the file I/O layer that pure-function tests don't touch. Requires either a real MP3 file committed to the repo or a synthesized minimal MP3 byte sequence.
    - AudioMirrorCommitter: the gating logic (skip on incremental, skip on dirty) has no return value so tests require either a refactor to return a status enum, or injecting a spy. The git operations require a temp git repo.
    - LibChecker exceptions mechanism: **done** - `LibCheckerExceptionTests.cs` covers wildcard, specific-match, and non-match cases (3 tests, all passing as of 2026-06-01).
  - **Scope discipline:** Test feature behavior, not individual function internals. "Artist casing is preserved end-to-end" not "ExtractAndFixArtists() branch 47". Internals are tested indirectly; changing internals should not break tests if behavior is unchanged.

- [ ] **XML serialization refactor (prerequisite for Phase 2.5)** - /think analysis 2026-06-03 found design flaws in the XML layer. Do this BEFORE Phase 2.5 to make the cover art schema redesign clean.
  - **Issue 1 - TrackXML : Track (wrong IS-A):** TrackXML inherits Track but it's a serializer, not a Track subtype. Fix: make `TrackXML` an `internal static class` with `Read(path, TrackTag)` and `Write(path, TrackTag)` methods. Use `XDocument`/`XElement` (System.Xml.Linq) instead of `XmlDocument` - eliminates XPath string fragility, no SetElementValue/SetChildElement wrappers needed. TrackTag constructor calls `TrackXML.Read(path, this)` - one line, no branching.
  - **Issue 4 - ParseCache FieldCount magic number:** `private const int FieldCount = 12` must stay in sync with the `Save()` field list and the 12-param TrackTag constructor. Phase 2.5 will break this. Fix: replace with a static `Extract(TrackTag t)` method that returns a `string[]` of all fields in order. `FieldCount` becomes `Extract(null).Length`. Save uses `string.Join(Sep, Extract(t))`. Deserialize validates `parts.Length == FieldCount` automatically. Adding/removing a field = one edit.
  - **Scope:** TrackXML.cs, ParseCache.cs. TrackXML tests need updating (behavior unchanged, just method signatures). No behavior changes, no force regen needed.
  - **Note:** The original Issue 2 (delete MP3-path branch in TrackTag) was a misdiagnosis - see HISTORY.md. Issue 3 (Track.cs backing fields -> auto-properties) is complete - see HISTORY.md.

- [ ] **Album art dimensions - Phase 3 (Schema redesign + Enforce)** - Phases 1 (Capture 2026-05-31) and 2 (Analyse 2026-06-02) done. Analysis run 2026-06-03: 5653/5653 covers present, 97 non-square. Histogram: 800x800=3038, 1200x1200=1712, 1000x1000=431, 500x500=84, 600x600=41, 700x700=26.
  - **Phase 2.5 (Schema redesign - do XML refactor above first):** Current schema stores only one Width/Height pair regardless of how many covers exist - when Count=2, whose dimensions are stored? Undefined. Redesign to zero-to-N collection: `<CoverArt>` wrapper with zero or more `<Cover width="N" height="N" />` child elements. Count is implicit (child element count - never stored separately). Changes: remove `AlbumCoverCount` / `CoverWidth` / `CoverHeight` fields from Track.cs, replace `<AlbumCover>` element with `<CoverArt>`/`<Cover width height>`, update XML writer (TrackXML), parser, TrackTag cache constructor / ParseCache Extract() method, TrackXML tests, AudioMirror-Format.md. Force regen required.
  - **Phase 3 (Enforce):** LibChecker rules after schema redesign: (a) **ERROR: no Cover children** (missing cover - always wrong); (b) **ERROR: any Cover where width != height** (non-square); (c) **WARNING: any Cover where min(width,height) < 800** (below library standard). Threshold rationale: 800x800 is 53.7% of the library - it is the established norm, not an arbitrary bar. Tracks below 800x800 = 151 (2.7%), likely old rips - WARNING not ERROR, exception-list suppressible. Do NOT use 500x500 as threshold; the data shows 800 is correct. Configurable in LibChecker config, not hardcoded.
  - 97 non-square covers flagged in 2026-06-03 run - investigate as separate task once Phase 3 rules are live (may be legitimate or fixable via mp3tag).

---

## TIER 3 - POLISH

**Goal: close structural gaps and improve developer experience. Non-blocking.**

- [ ] **Delete DecisionLog.cs dead code** - Class is retained but unused since 2026-05-25 TeeWriter refactor (HISTORY.md). TIER 4 "decision XML analysis" that would have used it has never been scheduled. Remove the class and its csproj entry.

- [ ] **MusicIntegrator constructor decomposition** - The public constructor is 400+ lines handling: TagFixer, scan-ahead, pre-scan, duplicate review, routing loop, dry-run output, JSON output, confidence report, misc migration, cleanup. Each phase should be its own private method. The constructor becomes a 15-line orchestrator. Purely a readability improvement; no behavior change.

- [ ] **CountAlbumSongs NewMusic re-reads** - `CountAlbumSongs` opens every NewMusic MP3 with TagLib# for each call. Called inside `GetDestDir` which runs once per file. For a batch of N files with M distinct albums, this is O(N x M) TagLib reads. RunScanAhead already built `batchCounts` from one TagLib pass. The batch-side count (how many tracks from this album are in this batch) should be derived from scan-ahead data, not re-read. Library-side count (album subfolder on disk) stays as-is.

- [ ] **LibChecker.GetRelPathPart bounds check** - `return GetPathParts(tag)[pos]` will throw `IndexOutOfRangeException` on any track with an unexpectedly short path. No production hit yet, but any malformed mirror XML would produce a crash in LibChecker. Guard with a length check or use `ElementAtOrDefault` with a fallback empty string.

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

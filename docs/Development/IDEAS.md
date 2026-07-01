# Ideas & Future Work

Single source of truth for all pending work. **CLI only.** GUI planning: `GUI-ROADMAP.md`. Completed items -> `HISTORY.md`.

Items are tiered by priority. Do not advance to the next tier until the current tier is verified on real data.

---

## TIER 1 - BLOCKING

**Goal: deliver auto-routing for known cases - eliminate confirmation fatigue. Prerequisites must be verified on real data first.**

**TIER 1 threshold:** anything that would cause a LibChecker warning belongs here, regardless of where or when it was discovered. Routing gaps, rule divergence, config omissions - all TIER 1 if LibChecker would fire on it.


- [ ] **Rename malformed multi-artist library files missing semicolon delimiter** - An audit on 2026-06-27 found ~436 library files where multi-artist filenames are missing the `;` delimiter (e.g. `T.I.Cee Lo Green - Hello.mp3`, `UsherAlicia Keys - ...`, `Polo GLil Wayne - ...`, `FredoDave - ...`). These are pre-TagFixer era imports where artists were concatenated without `;`. LibChecker has a check for this (code exists, currently commented out in LibChecker.cs) which will be re-enabled after remediation. Fix: use Mp3tag on Raphael bulk-rename using tag field `%artist% - %title%` for all affected files, which inserts correct `;` delimiters from ID3 tags. Then re-enable the LibChecker check. Must be done before enabling LibChecker validation. Known worst offenders: T.I. Singles, Polo G, Lil Wayne collaborations, Dave featured tracks.

- [ ] **Fix MP3 filename casing to match artist ID3 tags** - several files have filenames with old/inconsistent artist casing that diverges from the ID3 tag (e.g., `Bowling For Soup - 1985.mp3` but tag says `Bowling for Soup`; `24kgoldnDaBaby - Coco.mp3` but tag says `24kGoldn`; similarly `Iann Dior` vs `iann dior`, `JAY-Z` vs `Jay-Z`, `Kota the Friend` vs `KOTA The Friend`). Fix via Mp3tag on Raphael (master copy) - rename files to match the ID3 artist tag casing. This WILL fix all current LibChecker casing warnings (genuine mismatches, not false positives). Must be done before next integration so LibChecker is clean.

---

## TIER 2 - QUALITY

**Goal: improve UX, add test coverage, and audit metadata quality.**

- [ ] **Re-enable album art dimensions check in LibChecker** - `CheckAlbumCoverDimensions` was temporarily commented out (2026-06-24) to unblock integration while the existing library has ~150+ low-res tracks. Re-enable once album art remediation is done (see TIER 4 "Album art remediation" entry). Also: print a single summary line instead of one line per track - the full list floods terminal output and belongs in the report file only.

- [ ] **Automated tests - long-term: broad program coverage** - TagFixer tests (done) and routing tests (Tier 1) deliver the foundation first. This entry covers ongoing expansion once routing tests are stable. Expand only when a real bug escapes current test coverage - never speculatively.
  - **Motivation (unchanged):** Each fix session currently requires 2-3 manual dry run + force regen cycles. Every module covered by a test eliminates that cycle for that module. Real integration (May 2026) found metadata edge cases dry-run missed - tests for the same logic would have caught several earlier.
  - **Infrastructure in place:** Inline `--test` flag, 20-line Assert class, test.bat, launch.bat integration. DIY - no xUnit, no separate project. Old-style csproj manual registration + no VS test runner in the build workflow makes a framework overkill.
  - **Expansion rule:** Add a test when a real bug escapes current coverage. Not before.
  - **Current coverage (comprehensive as of 2026-07-01):** TagFixer (pure functions + null guards + full-pipeline ProcessFile with synthesized MP3 fixture), routing (all paths including ATD 4 paths, scan-ahead, album thresholds, compilation albums), ParseCache (round-trip, staleness, corruption), LibChecker (all rules including Compilations/ validation), Track.ProcessProperty, StatList (GetSortedFreqDist + GetDecadeFreqDist), TrackXML (round-trip + special chars + missing element), Parser (reads XMLs, skips README, throws on non-XML, cache hit), Reflector (SanitiseFilename all invalid char behaviors + case preservation, IsStaleMirrorXml stale detection), AgeChecker (all 5 branches), TeeWriter (capture writer, file output, per-line timestamps, embedded newline invariant), DuplicateDetection (artist-alias loading and key expansion).
  - **Remaining expansion candidates (in value order):**
    - AudioMirrorCommitter: the gating logic (skip on incremental, skip on dirty) has no return value so tests require either a refactor to return a status enum, or injecting a spy. The git operations require a temp git repo.
    - LibChecker exceptions mechanism: **done** - `LibCheckerExceptionTests.cs` covers wildcard, specific-match, and non-match cases (3 tests, all passing as of 2026-06-01).
  - **Scope discipline:** Test feature behavior, not individual function internals. "Artist casing is preserved end-to-end" not "ExtractAndFixArtists() branch 47". Internals are tested indirectly; changing internals should not break tests if behavior is unchanged.

---

## TIER 3 - POLISH

**Goal: close structural gaps and improve developer experience. Non-blocking.**

- [ ] **ParseCache mtime check doesn't detect deletions within same second** - IsMirrorStale() checks if any XML mtime is newer than cache. But if an XML is deleted and recreated within the same second, the check might miss it (same mtime). Low probability, but possible. Consider: track file count in cache header as well as mtime.


- [ ] **ReportWriter year-folder accumulation** - Reports use date-based filenames (`yyyy-MM-dd`), so same-day reports overwrite (bounded growth per day). Growth concern is long-term: one `reports/{year}/` folder per year accumulates indefinitely. Consider: prune folders older than N years, or keep only the last N clean reports regardless of year. Low urgency - years of reports are small text files.

- [ ] **TagFixer artist separator diverges from TagLib# JoinedPerformers format** - TagFixer line 99 joins artists with `";"` (no space: `string.Join(";", artistList)`) for the `artistsChanged` comparison and filename rename. TagLib#'s `JoinedPerformers` property joins Performers arrays with `"; "` (semicolon space). If an incoming file has Performers stored as a TagLib# array `["Artist1", "Artist2"]` (e.g. a file previously processed by TagFixer), `artistsChanged` fires unnecessarily, the file gets renamed to `"Artist1;Artist2 - Title.mp3"` (no space), but the ID3 tag reads back as `"Artist1; Artist2"` (with space) → LibChecker `CheckFilenamesCasingsMatchArtistTags` mismatch. Has not manifested in the 2026-06-28 batch (all arriving files had single-string Performers, no space needed). **Investigation 2026-06-28:** confirmed no current mismatch - on-disk filename and `<Artists>` tag both use `"; "` consistently. Fix: align TagFixer's join separator with TagLib#'s output, or normalize both sides before comparison.

- [ ] **Stub-file pattern in Reflector is vestigial** - Reflector creates text files with just MP3 paths (line 156 in Reflector.cs), but these are immediately overwritten by TrackXML with actual XML content. The stubs are never read as input - they're just a temporary placeholder. Current architecture: Reflector writes stub, Parser reads MP3 via TagLib#, TrackXML overwrites stub with XML. Alternative: Reflector could directly call TrackXML, skipping the stub stage. Requires: Reflector knowing how to extract ID3 tags (it currently doesn't). Lower priority - works as-is, but worth considering if parsing performance becomes an issue.

- [ ] **Rules unification: single RulesEngine for MusicIntegrator + LibChecker** - Rules currently defined separately in GetDestDir and LibChecker; divergence has caused "TagFixer SKIPPED but LibChecker FLAGGED" bugs. Refactor into a shared RulesEngine consumed by both. **Promoted from TIER 4 (2026-07-01):** the dry-run projection (shipped) now surfaces divergences automatically, satisfying the trigger condition this item was waiting on, and the B.I.G. album-suffix case (2026-06-28) is a second real instance of the same divergence. The album-version normalizer is the first concrete shared rule that belongs in the unified engine.

---

## TIER 4 - FUTURE

**Goal: exploratory features and advanced enhancements, tackled after core tiers are stable.**

- **AudioMirror schema formalization (XSD)** - Formalise the current XML schema (AudioMirror-Format.md) as an XSD file (`AudioMirror-Schema.xsd` at repo root). Not needed now; would be valuable if AudioManager becomes a library or other tools consume the format. Minimal benefit for internal-use-only repo. Consider if schema ever diverges across users or if external tooling emerges.

- **Pattern analysis: extract routing patterns from decision XMLs + AudioMirror data** - Artist folder distribution, album patterns, genre consistency. Build statistical models to identify high-confidence auto-routing cases. Blocked by: need real integration decision XML data from multiple runs.

- **Refactor CountAkiraTheDonPersonSongs/AlbumSongs - artist as parameter** - [code smell] `CountAkiraTheDonPersonSongs(sampledPerson)` and `CountAkiraTheDonPersonAlbumSongs(sampledPerson, album)` hardcode the artist ("Akira The Don") in the function name. These should become `CountPersonSongs(artist, sampledPerson)` and `CountPersonAlbumSongs(artist, sampledPerson, album)` so the same logic can serve future artists with the same People/ folder structure. Only matters if a second artist with a People/-style structure is added.

- **Sources/ routing not implemented in GetDestDir()** - `Constants.SourcesDir` exists but `MusicIntegrator.GetDestDir()` has no routing logic for Sources/Films, Sources/Shows, or Sources/Anime. Films/Shows/Anime tracks currently fall to Misc and require manual folder-picker redirection. **Current decision: manual folder-picker is acceptable long-term** - metadata alone rarely indicates source type reliably, and Sources/ tracks are infrequent enough that automation doesn't pay for itself. Only revisit if Sources/ intake volume increases significantly.


- **Routing decision analysis mode** - Add a mode that reads decision XMLs, cross-references routing decisions against routing rules code and LibChecker rules, and flags inconsistencies. Produces a report: "these N files were routed to X but LibChecker would flag them as Y". Pairs well with the "Centralise rules" refactor. Exploratory - assess value after the first real integration run produces decision XML data to analyse.

- **Pipeline transaction semantics** - Current analysis pipeline has no rollback: Reflector writes stubs, Parser reads MP3s and caches, TrackXML writes XMLs, Analyser generates stats. If a crash occurs between stages, the mirror is in a partial state. Consider: (a) write all XMLs to a staging directory, then atomic move to real directory on success, or (b) add a "verify mirror consistency" stage before auto-commit. Incident prevention: add integration test that simulates crash mid-pipeline and checks recovery.

- **ParseCache format is not version-resilient** - ParseCache uses header "PARSE_CACHE_V1" but no version bump mechanism if the schema changes. If a new field is added to TrackTag (e.g., new metadata), old cache files become invalid but are still loaded as V1. Consider: include schema version in header ("PARSE_CACHE_V2_SCHEMA_XYZ") so old caches are clearly stale and rejected.

- **Analyser report generation is not incremental** - Every analysis run re-generates the full stats report from all 5653 tags. For a library of this size, could optimize: cache decade/genre/artist histograms from previous run, only update changed tags. Possible only if cache includes enough metadata. Low priority - current performance acceptable.

- **LibChecker has hardcoded folder names** - References Compilations/, Musivation/, Motivation/, Artists/, Miscellaneous Songs/, Sources/ as string literals in code. If folder structure changes, these hardcodes break. Consider: read expected folders from configuration file (e.g. library-structure.json) instead of hardcoding.

- **No dry-run mode for analysis** - Integration has --dry-run, but analysis does not. If you want to see what a force-regen would do without actually writing files, you can't. Lower priority - force-regen is generally safe and the preview would just be "will regenerate X files", not particularly informative.

- **"My Edits" tracking** - detect locally edited songs by comparing duration to official track (>3-4s diff = protected from overwrite).
- **Parody/original song pairing detection** - flag songs where a parody and its original are both in the library.
- **Album completion detection** - cross-reference library against Spotify/MusicBrainz; flag where 50%+ of an album is owned.
- **Fuzzy artist name matching** - handle artist name variations during routing ("The Beatles" vs "Beatles", featured artist formatting differences). Only matters at scale.
- **Fuzzy duplicate title matching (partial)** - feat./ft./featuring stripped from both sides. Remaining gap: "Song - Remix" vs "Song" still won't match (by design - remixes are different tracks). Could extend to strip "(Live)", "(Acoustic)" etc. if false-negatives appear.
- **Neural network routing (exploratory)** - Train a simple neural network on AudioMirror library commit history and routing decisions to learn implicit routing patterns instead of defining everything statically. Model input: track metadata (artist, album, tags, file structure). Model output: routing destination. Payback: reduces boilerplate routing rules, evolves with library patterns. Very low priority, exploratory phase only - assess whether domain patterns are learnable and whether ML overhead justifies the benefit.

- **Album art remediation (Phase 4) - fix sub-800 covers** - LibChecker now flags covers below 800x800. Phase 4: act on those flags. Decision needed first: (a) upscale existing art in-place (no internet, quality risk), (b) fetch higher-res art from MusicBrainz/Cover Art Archive (internet required, best quality), (c) manual mp3tag workflow with no automation. If (a) or (c) is sufficient, implement as AM CLI command. If (b), evaluate: AM integration vs. standalone accept/reject tool with minimal GUI. First step: spot-check how many of the ~151 sub-800 tracks have art that can be upscaled vs. truly needs a fresh fetch.

- **Lyrics enrichment - fetch and embed lyrics from external sources** - Connect to a metadata source (Genius API, MusicBrainz, or AZLyrics) to fetch lyrics and embed in ID3 tags (`USLT` frame). New mode: `--enrich-lyrics`. Prerequisite for lyric search. Design choice: batch all tracks vs. on-demand per track (start on-demand, expand to batch later). Add `<Lyrics>` element to AudioMirror XML schema alongside existing metadata.

- **Fuzzy lyric search** - Search the library by lyric fragment. Match partial/approximate text against `<Lyrics>` elements in AudioMirror XMLs. New mode: `--search-lyrics "some fragment"`. Output: ranked matches. Implementation: normalise text (lowercase, strip punctuation), then Levenshtein distance or n-gram similarity for fuzzy matching. Depends on lyrics enrichment being in place first.

---

## See Also

- `docs/Development/HISTORY.md` - completed features, settled design decisions, parked ideas
- `docs/Development/GUI-ROADMAP.md` - GUI planning: webapp, tabs, Sonarr/Radarr vision, far-future integrations
- `docs/References/Music-Library-Rules.md` - canonical rules for library structure
- `docs/References/Post-Integration-Validation.md` - why post-integration LibChecker warnings are often ghost-XML false positives, the dry-run projection fix, and the 2026-06-28 run analysis
- `docs/Historical/NewMusic-Integration-Plan-20260308.md` - past batch integration (March 2026 batch A)
- `docs/Historical/NewMusic-Integration-Plan-20260407.md` - past batch integration (April 2026)
- `docs/References/AudioMirror-Format.md` - AudioMirror XML format and repo info

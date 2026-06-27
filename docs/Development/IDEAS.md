# Ideas & Future Work

Single source of truth for all pending work. **CLI only.** GUI planning: `GUI-ROADMAP.md`. Completed items -> `HISTORY.md`.

Items are tiered by priority. Do not advance to the next tier until the current tier is verified on real data.

---

## TIER 1 - BLOCKING

**Goal: deliver auto-routing for known cases - eliminate confirmation fatigue. Prerequisites must be verified on real data first.**

**TIER 1 threshold:** anything that would cause a LibChecker warning belongs here, regardless of where or when it was discovered. Routing gaps, rule divergence, config omissions - all TIER 1 if LibChecker would fire on it.

- [x] **[FIXED 2026-06-27] Scan-ahead album key mismatch caused wrong Singles routing** - `RunScanAhead` stored raw album tags ("The Blueprint (Explicit Version)") but `CountAlbumSongs` queried by normalized name ("The Blueprint"). Mismatch caused 0 batch songs found for every album with a suffix -> routed to Singles instead of album subfolder. Fixed: normalize album in `RunScanAhead` before storing. Tests added: `Routing_BatchAlbumCount_NormalizedAlbumSuffixMatchesCount`. -> HISTORY.md

- [x] **[FIXED 2026-06-27] Self-titled album exclusion prevented Paperboys from getting album folder** - `GetDestDir` had `!track.Album.Equals(primaryArtist2)` condition that treated self-titled albums (album name == artist name) as "no distinct album", routing them to Singles even with 6 batch songs. Fixed: removed the condition; album count alone determines routing. Tests added: `Routing_SelfTitledAlbum_WithLibrarySongs_RoutesToAlbumFolder`. -> HISTORY.md

- [x] **[FIXED 2026-06-27] Artist casing normalization destroyed abbreviations** - `ToTitleCase(a.ToLower())` turned "PJ Simas" -> "Pj Simas" and "XV" -> "Xv". Mixed-case names must be preserved as-is. Fixed: skip `.ToLower()` for mixed-case names (upper + lower both present); added "XV" and "PJ Simas" to overrides config. Tests added: `ExtractAndFixArtists_MixedCaseAbbreviation_Preserved`, `ExtractAndFixArtists_AllCapsAbbreviation_InOverrides_Preserved`. -> HISTORY.md

- [ ] **[BLOCKING] Duplicate detection misses artist-rename scenarios (Ye/Kanye West)** - Ye tracks from "BULLY - DELUXE" were not flagged as duplicates of existing "BULLY" songs stored under "Kanye West" in the library. The duplicate checker uses `primaryArtist + title` key: if the library has "kanye west" but the new file has "ye", no match is found. The old BULLY album had to be manually deleted. **Fix needed:** artist alias mapping (e.g. `artist-aliases.xml` config: "Kanye West" -> "Ye"). Without it, any artist who changes their name will silently create duplicates on the next integration. Low-frequency but high-impact: misses are real and library gets duplicate tracks.

- [ ] **[BLOCKING] AudioMirrorCommitter hangs on force-regen runs** - Audio analysis + force-regen completes successfully (LibChecker clean, 5653 files staged), but AudioMirrorCommitter.cs hangs waiting to commit. Program output stops at "AudioMirror auto-commit:" with no further progress. Process must be manually killed. **Reproduce:** run `analysis --force-regen` and wait. **Root cause unknown.** Affects: auto-commit logic (likely git operation or subprocess hanging). **Workaround:** manual commit after force-regen (`git commit -m "sync: AudioMirror regeneration"`). **Improved tests needed:** add integration test that: (1) runs force-regen, (2) verifies LibChecker clean, (3) verifies auto-commit completed without hanging. Test should timeout if no git commit completes within N seconds (catch hangs early).

- [ ] **Fix MP3 filename casing to match artist ID3 tags** - several files have filenames with old/inconsistent artist casing that diverges from the ID3 tag (e.g., `Bowling For Soup - 1985.mp3` but tag says `Bowling for Soup`; `24kgoldnDaBaby - Coco.mp3` but tag says `24kGoldn`; similarly `Iann Dior` vs `iann dior`, `JAY-Z` vs `Jay-Z`, `Kota the Friend` vs `KOTA The Friend`). Fix via Mp3tag on Raphael (master copy) - rename files to match the ID3 artist tag casing. This WILL fix all current LibChecker casing warnings (genuine mismatches, not false positives). Must be done before next integration so LibChecker is clean.

---

## TIER 2 - QUALITY

**Goal: improve UX, add test coverage, and audit metadata quality.**

- [ ] **Routing output: show album in each block** - Dry-run output shows `[AUTO] Artist - Title` but not the album. User needs to see album to verify routing decisions without cross-referencing the library. Design: `[AUTO] Jay-Z - Izzo (H.O.V.A.) [The Blueprint]` on the header line (album in brackets), OR a separate ` > Album: "The Blueprint"` line only when the album is relevant to the routing decision (i.e. when going to an album subfolder). Do not add album for Singles/Misc routes where it's not useful. Keep the display clean - avoid the verbose `Artist: / Title: / Album:` multi-line format.

- [ ] **Routing output: simplify [AUTO] / [AUTO - new folder] labels** - `[AUTO - new folder]` is noisy and not very meaningful to the user. Consider: remove the new-folder variant entirely, or collapse to a single indicator on the Route line ("* new" suffix). The new-folder info is already visible in the scan-ahead summary at the top.

- [ ] **Re-enable album art dimensions check in LibChecker** - `CheckAlbumCoverDimensions` was temporarily commented out (2026-06-24) to unblock integration while the existing library has ~150+ low-res tracks. Re-enable once album art remediation is done (see TIER 4 "Album art remediation" entry). Also: print a single summary line instead of one line per track - the full list floods terminal output and belongs in the report file only.

- [ ] **LibChecker detection via output capture is fragile** - Program.cs line 150 detects "LibChecker: Clean" by searching captureWriter output. If LibChecker output format changes, detection silently breaks - auto-commit could fire on a dirty library. Fix: LibChecker returns `IsClean` bool (already exists as a property) and Program.cs reads that instead of string-searching stdout.

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

---

## TIER 3 - POLISH

**Goal: close structural gaps and improve developer experience. Non-blocking.**

- [ ] **MusicIntegrator constructor decomposition** - The public constructor is 400+ lines handling: TagFixer, scan-ahead, pre-scan, duplicate review, routing loop, dry-run output, JSON output, confidence report, misc migration, cleanup. Each phase should be its own private method. The constructor becomes a 15-line orchestrator. Purely a readability improvement; no behavior change.

- [ ] **TrackXML.Write should use LF line endings** - `XDocument.Save(path)` on Windows writes CRLF, but `.gitattributes` forces `*.xml eol=lf` storage. Every force-regen marks all 5,000+ XMLs as dirty until committed. Fix: replace `.Save(path)` with an `XmlWriter` configured with `NewLineChars = "\n"` (one-line change in `TrackXML.cs:52`). This eliminates the "5,654 dirty files" state after every regen, even without fixing the AudioMirrorCommitter hang.

- [ ] **XML file write should use temp-file pattern** - TrackXML.Write() calls .Save() directly on the target path. If the process crashes mid-write, the XML file is corrupted. Better: write to a temp file, then atomic move/rename. Protects against partial writes.

- [ ] **ParseCache mtime check doesn't detect deletions within same second** - IsMirrorStale() checks if any XML mtime is newer than cache. But if an XML is deleted and recreated within the same second, the check might miss it (same mtime). Low probability, but possible. Consider: track file count in cache header as well as mtime.

- [ ] **ReportWriter timestamps accumulate on disk** - Every analysis run creates a new timestamped report. Over many runs, the reports/ directory grows unbounded. Consider: (a) cleanup policy (keep last N or last N days), (b) compress old reports, or (c) store in a database instead of individual files.

- [ ] **Stub-file pattern in Reflector is vestigial** - Reflector creates text files with just MP3 paths (line 156 in Reflector.cs), but these are immediately overwritten by TrackXML with actual XML content. The stubs are never read as input - they're just a temporary placeholder. Current architecture: Reflector writes stub, Parser reads MP3 via TagLib#, TrackXML overwrites stub with XML. Alternative: Reflector could directly call TrackXML, skipping the stub stage. Requires: Reflector knowing how to extract ID3 tags (it currently doesn't). Lower priority - works as-is, but worth considering if parsing performance becomes an issue.

- [ ] **Album art stats in terminal output - reduce verbosity** - the album art width/height histogram (e.g., "800x800=3038, 1200x1200=1712...") is printed on every analysis run. Now that LibChecker flags sub-800 covers, the full histogram adds noise. Options: (a) collapse to one line ("N tracks below 800px, M non-square"), (b) move full histogram to report file only and print summary to terminal, (c) add `--verbose` flag to show full histogram on demand. Goal: terminal output stays scannable without losing the detail.

---

## TIER 4 - FUTURE

**Goal: exploratory features and advanced enhancements, tackled after core tiers are stable.**

- **AudioMirror schema formalization (XSD)** - Formalise the current XML schema (AudioMirror-Format.md) as an XSD file (`AudioMirror-Schema.xsd` at repo root). Not needed now; would be valuable if AudioManager becomes a library or other tools consume the format. Minimal benefit for internal-use-only repo. Consider if schema ever diverges across users or if external tooling emerges.

- **Pattern analysis: extract routing patterns from decision XMLs + AudioMirror data** - Artist folder distribution, album patterns, genre consistency. Build statistical models to identify high-confidence auto-routing cases. Blocked by: need real integration decision XML data from multiple runs.

- **Rules unification: single RulesEngine for MusicIntegrator + LibChecker** - Rules currently defined separately in GetDestDir and LibChecker; divergence has caused "TagFixer SKIPPED but LibChecker FLAGGED" bugs. Refactor into a shared RulesEngine consumed by both. Prerequisites: routing stable, patterns understood.

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
- **Fuzzy duplicate title matching** - extend the pre-integration duplicate check to catch near-matches (e.g. "Song (feat. X)" vs "Song", "Song - Remix" vs "Song"). Approach: normalise both sides before comparison by stripping featured artist parentheticals, stripping remix/edit/version suffixes, collapsing whitespace. Blocked by: the exact-match duplicate check must be in place first.
- **Neural network routing (exploratory)** - Train a simple neural network on AudioMirror library commit history and routing decisions to learn implicit routing patterns instead of defining everything statically. Model input: track metadata (artist, album, tags, file structure). Model output: routing destination. Payback: reduces boilerplate routing rules, evolves with library patterns. Very low priority, exploratory phase only - assess whether domain patterns are learnable and whether ML overhead justifies the benefit.

- **Album art remediation (Phase 4) - fix sub-800 covers** - LibChecker now flags covers below 800x800. Phase 4: act on those flags. Decision needed first: (a) upscale existing art in-place (no internet, quality risk), (b) fetch higher-res art from MusicBrainz/Cover Art Archive (internet required, best quality), (c) manual mp3tag workflow with no automation. If (a) or (c) is sufficient, implement as AM CLI command. If (b), evaluate: AM integration vs. standalone accept/reject tool with minimal GUI. First step: spot-check how many of the ~151 sub-800 tracks have art that can be upscaled vs. truly needs a fresh fetch.

- **Lyrics enrichment - fetch and embed lyrics from external sources** - Connect to a metadata source (Genius API, MusicBrainz, or AZLyrics) to fetch lyrics and embed in ID3 tags (`USLT` frame). New mode: `--enrich-lyrics`. Prerequisite for lyric search. Design choice: batch all tracks vs. on-demand per track (start on-demand, expand to batch later). Add `<Lyrics>` element to AudioMirror XML schema alongside existing metadata.

- **Fuzzy lyric search** - Search the library by lyric fragment. Match partial/approximate text against `<Lyrics>` elements in AudioMirror XMLs. New mode: `--search-lyrics "some fragment"`. Output: ranked matches. Implementation: normalise text (lowercase, strip punctuation), then Levenshtein distance or n-gram similarity for fuzzy matching. Depends on lyrics enrichment being in place first.

---

## See Also

- `docs/Development/HISTORY.md` - completed features, settled design decisions, parked ideas
- `docs/Development/GUI-ROADMAP.md` - GUI planning: webapp, tabs, Sonarr/Radarr vision, far-future integrations
- `docs/References/Music-Library-Rules.md` - canonical rules for library structure
- `docs/Historical/NewMusic-Integration-Plan-20260308.md` - past batch integration (March 2026 batch A)
- `docs/Historical/NewMusic-Integration-Plan-20260407.md` - past batch integration (April 2026)
- `docs/References/AudioMirror-Format.md` - AudioMirror XML format and repo info

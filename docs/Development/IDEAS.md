# Ideas & Future Work

Single source of truth for all pending work - CLI and GUI both, tagged `[GUI]` where relevant, in the same priority tiers below. Not a home for architecture/design writeups: those live in `docs/References/GUI-Architecture.md` (GUI stack/tab reference), `fable-gui/fable-brief.md` (Fable build brief), `docs/References/` (format specs). Completed items -> `HISTORY.md`.

Items are tiered by priority. Do not advance to the next tier until the current tier is verified on real data.

**Fable session status (2026-07-03, promo window closes 2026-07-07):** round 2 complete - all five TIER 1-2 `[GUI]` items closed (layout fix, fluid UI, `--manifest` selective integration, Mirror commit action, mood-reactive theme; see HISTORY.md 2026-07-03). Commits on `main` not yet pushed (David's job). `Hagrid & Harry` TCMP fix confirmed done (2026-07-03) - the LibChecker gate-block is resolved, integration flows again.

---

## TIER 1 - BLOCKING

**Goal: deliver auto-routing for known cases - eliminate confirmation fatigue. Prerequisites must be verified on real data first.**

**TIER 1 threshold:** anything that would cause a LibChecker warning belongs here, regardless of where or when it was discovered. Routing gaps, rule divergence, config omissions - all TIER 1 if LibChecker would fire on it.


- [ ] **Rename malformed multi-artist library files missing semicolon delimiter** - An audit on 2026-06-27 found ~436 library files where multi-artist filenames are missing the `;` delimiter (e.g. `T.I.Cee Lo Green - Hello.mp3`, `UsherAlicia Keys - ...`, `Polo GLil Wayne - ...`, `FredoDave - ...`). These are pre-TagFixer era imports where artists were concatenated without `;`. LibChecker has a check for this (code exists, currently commented out in LibChecker.cs) which will be re-enabled after remediation. Fix: use Mp3tag on Raphael bulk-rename using tag field `%artist% - %title%` for all affected files, which inserts correct `;` delimiters from ID3 tags. Then re-enable the LibChecker check. Must be done before enabling LibChecker validation. Known worst offenders: T.I. Singles, Polo G, Lil Wayne collaborations, Dave featured tracks.
  - **Cross-repo connection found (2026-07-05, via Constellation's Fable build):** Constellation (new repo, renders the library as a 3D artist graph) surfaced real, additional instances of this same family while playtesting against the real library - KRS-One (`KRS-OneMarley Marl`, `KRS-OneSpecial K` - no delimiter at all in the ARTIST TAG column itself, not just the filename) and Kota the Friend (same artist already listed above for casing, but ALSO showing `\` and `/` as artist separators on different albums, not just missing them entirely). **Important distinction, not yet confirmed:** this audit's fix targets FILENAME delimiters (reading correct data from the ID3 tag to fix the filename); Constellation's evidence suggests the TAG VALUES THEMSELVES (the `Artist`/`Artists` ID3 fields Mp3tag shows directly, which is what `Track.ProcessProperty()` and `tracks.json` actually read) may have the same missing/wrong-delimiter problem on at least some tracks, not just the filenames - worth checking whether remediation needs to touch tag data, not only rename files, when this item is picked up. `Track.ProcessProperty()` (`Track.cs` lines 49-58) only recognizes `;`/`,` as separators and silently returns the whole unsplit string for anything else (including `PrimaryArtist`, which reads from the same method) - full technical writeup and more examples in `Constellation/docs/Development/IDEAS.md`.

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

- [ ] **[GUI] TagFix configurable rules** - Tag Fix tab currently shows the exe's fixed transforms only (`tagfix --dry-run`). Needs a C# change to accept user-defined rules (condition -> fix); see the rule-builder design in `docs/References/GUI-Architecture.md`.

- [ ] **[GUI] Library Browser polish** - MVP is table/grid + search/chips/pagination; still missing: track detail panel (full tags, file path), multi-select for batch TagFix apply.

- [ ] **[GUI] Metadata enrichment tab - album art review/approve workflow** - added 2026-07-14.

  **Raw (David, verbatim, typos fixed):** "AudioManager GUI metadata enrichment tab, use for album art improvement - show current song metadata, find better album art, say yes or no. See album art resolution issue in ideas or recorded somewhere. Would be nice to have GUI where I can see the current song metadata and the proposed new album art pulled from some metadata library, or multiple possibilities, and I can say No to all, or say Yes to one of them and the song automatically gets its metadata updated with the new album art - will prevent me from having to do all this manually looking at metadata and googling each one."

  **Processed:** New GUI tab (Services tab or its own tab - see design note below) for reviewing and approving album art fixes one track at a time:
  - Panel shows the track's current metadata (artist, album, title, current embedded art thumbnail) side by side with one or more candidate covers pulled from an external metadata source (MusicBrainz/Cover Art Archive, per TIER 4 "Album art remediation" option (b) below).
  - Multiple candidates can be shown per track if the source returns more than one match (different releases/pressings/resolutions) - user picks one or rejects all.
  - Actions: **No to all** (skip, leave art untouched, move to next track) or **Yes on one candidate** (embed that art into the file's ID3 tag immediately, no separate confirmation step).
  - Queue driven off the existing sub-800px low-res list already flagged by `CheckAlbumCoverDimensions` in LibChecker (currently commented out per TIER 2 "Re-enable album art dimensions check" above) - this tab is the natural front-end for working through that backlog track-by-track instead of a bulk/automated fetch.
  - Replaces the current fully-manual workflow: opening each track, reading its tags, googling the cover art by hand, and embedding it manually via Mp3tag.
  - Directly related to / front-end for TIER 4 "Album art remediation (Phase 4)" below - that entry frames the fetch-source decision (a/b/c); this tab is the accept/reject UI specifically for option (b) once a fetch source is chosen. Should be scoped together with that decision, not built before it.

- [ ] **[GUI] Audio player - basic local playback (iTunes-like, next concrete build milestone)** - added 2026-07-03. Play the library directly from the GUI: play/pause, seek, volume, a persistent Now Playing bar (not a new tab), queue built from whatever's currently selected in Library Browser or Statistics drill-downs. Scope deliberately bounded for a single Fable push:
  - IN: local playback only, one audio backend (NAudio - already listed in `GUI-Architecture.md` third-party candidates), single Now Playing bar reusing existing Library Browser rows for track selection.
  - OUT (explicitly, this round): syncing, AI DJ / smart queue, ratings/reviews, mobile - these are the `custom-iphone-music-app` directive's territory (`PRIVATE_NOTES/roadmap/directives/custom-iphone-music-app.md`, currently blocked on Mac/Xcode access) and shouldn't be pulled into this GUI's scope prematurely.
  - Why bounded: this is meant to be the last major feature push of the current Fable session, not the start of an open-ended player subsystem - ship it, verify it, stop.

---

## TIER 3 - POLISH

**Goal: close structural gaps and improve developer experience. Non-blocking.**

- [ ] **[GUI] Rename the GUI to something better than "AudioManager GUI"** - added 2026-07-04, David's own note during a Fable strategy session. No name decided yet - revisit when doing a public-facing pass on the GUI (README, GitHub description).

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

- **[GUI] Services tab data sources** - Services tab is currently two placeholder stub cards. Should support any combination of: (1) Last.fm (scrobble history, listening stats, play counts overlaid on Library Browser), (2) Spotify (via SpotifyPlaylistGen or a generalized SpotifyTools lib - decision deferred, see `docs/References/GUI-Architecture.md`), (3) offline library/AudioMirror (already the only source today). Design goal: any combination usable at once (e.g. offline + Last.fm without Spotify) - each an independent, toggleable data source feeding one cross-synthesis view (owned-but-unplayed, played-but-not-owned, etc.), not an all-or-nothing switch.

- **[GUI] Multi-user path** - AudioManager is built around one person's library and one hardcoded AudioMirror repo path. To eventually serve other users: (1) make the library mirror's git repo configurable - point at an arbitrary existing remote, or initialize an AudioMirror-style repo inside the app's own data directory on first run with no external GitHub account required; (2) generalize routing rules and folder-structure assumptions (LibChecker hardcoded folder names, above) away from David's specific layout. Prerequisite for any other user running the tool, independent of the hosting question below.

- **[GUI] Proper hostable web service** - account system, logins, multi-tenant, usable by other people over the internet (not just localhost). Freemium model: core local tool free, hosted/sync features paid. The actual commercialization path if the AudioManager business-plan review (see workspace `pending-actions.md`) comes back positive. Big lift - needs auth, per-user data isolation, and the multi-user path above done first.

- **Rewrite core in Python** - eliminate the current JSON/XML double-up (C# writes XML to AudioMirror + JSON contract to the GUI) by having one language own both the data layer and the interface. Not scoped, not urgent - the current subprocess+JSON-contract architecture (decided 2026-07-03, see `docs/References/GUI-Architecture.md`) already isolates the GUI from the XML entirely, so this is a "someday, if maintaining two languages becomes real pain" idea, not a fix for a current problem.

- **Centralise the entire desktop music workflow (far future, not scoped)** - added 2026-07-03. Once basic playback lands (TIER 2 above), the natural next question: could AudioManager become the one place for everything David does with his music library on PC - listening, organizing, syncing - instead of that being spread across AudioManager (organize) + Windows Media Player/other player (listen) + iTunes (phone sync)? This is the desktop-side sibling of the `custom-iphone-music-app` directive's mobile-side vision (unified playback, AI DJ, blended Spotify+offline) - same underlying pain (fragmented tools for one library), different device. Not scoped and not Fable-ready as-is: needs a Sonnet pass first to define what "centralise" actually means in bounded terms (replace Windows' default player entirely? just add playback? add sync orchestration?) before it's buildable. Revisit after the basic audio player ships and proves out the player-in-GUI pattern.

- **Modernize AudioMirror Storage Contract (XML -> JSON / Compiler Pattern)** - AudioMirror stores one raw XML file per track: great for Git diffs and conflict-free merges, but XML is verbose, needs character escaping (`&amp;` etc.), and parsing thousands of separate files is a disk I/O bottleneck at GUI startup (mitigated today by the compiled `tracks.json`/`analysis-stats.json` contract, but the underlying per-track store is still XML). Proposed: (1) migrate per-track storage from XML to flat JSON (`<track>.json`) - same one-file-per-track Git-diff-friendly layout, less overhead, no escaping; (2) formalize the "Compiler Pattern" the C# Analyser already does informally - ingest the decentralized per-track JSONs and output one optimized runtime cache (unified `tracks.json` array, or SQLite/DuckDB if query needs grow past flat-file scans); (3) the Python GUI keeps strictly reading the compiled runtime cache only, same as today. Evolution of the current architecture, not a rewrite - the read/write contract (GUI never touches per-track files directly) is already in place.

---

- **[GUI] Investigate: 3D/liquid/gradient UI effect libraries for visual polish** - added 2026-07-11, investigate later, do not build now. David found GitHub repos/skill collections for advanced frontend effects (shader gradients, liquid-glass, animation patterns) that could apply to the GUI's visual polish once the audio player milestone ships. Full link list and stack-fit notes in `PRIVATE_NOTES/memory/reference/frontend-ui-design-reference.md` - check against this GUI's actual framework (`docs/References/GUI-Architecture.md`) before scoping.

## See Also

- `docs/Development/HISTORY.md` - completed features, settled design decisions, parked ideas
- `docs/References/GUI-Architecture.md` - GUI architecture/design reference: stack decision, tab table, third-party libs
- `docs/References/Music-Library-Rules.md` - canonical rules for library structure
- `docs/References/Post-Integration-Validation.md` - why post-integration LibChecker warnings are often ghost-XML false positives, the dry-run projection fix, and the 2026-06-28 run analysis
- `docs/Historical/NewMusic-Integration-Plan-20260308.md` - past batch integration (March 2026 batch A)
- `docs/Historical/NewMusic-Integration-Plan-20260407.md` - past batch integration (April 2026)
- `docs/References/AudioMirror-Format.md` - AudioMirror XML format and repo info

# History

Completed features, settled design decisions, resolved tasks, and decisions explicitly not implemented.

---

## 2026-05-07 - Stage 3C Retrospectives Complete

Comprehensive post-integration analysis completed for Stage 3C (April 26 - May 7, 2026, 11 days, 251 commits).

**Forensic Analysis:** `docs/Development/FEEDBACK-Stage3C-2026-05-07.md` (403 lines)
- Full timeline of execution, blocker discoveries, root cause analysis, pattern analysis, confidence assessment, process insights
- Key findings: 3-blocker cluster identified (illegal characters, artist casing, album suffixes), dry-run effective for normal cases but misses statistical outliers (edge cases found in real integration), scope evolution tracked (82 IDEAS updates = 33% of commits)
- Deliverable: TIER 2+ work prioritized by findings and validated by 502 real integration outcomes (Musivation batch, Stage 3C blocker fixes verified)
- Impact: All downstream TIER 2 items now cross-referenced to retrospective root cause analysis and evidence

**Claude Workspace Reflection:** `ClaudeOnly/memory/overnight-reflections/2026-05-07-Stage3C-reflection.md` (650 lines)
- Post-mortem analysis of Claude's performance during AudioManager Stage 3C (12 /dev-session invocations, 247 commits)
- Decision audit: validated decisions (character sanitization, artist casing overrides, routing logic, decision logging), partially validated decisions (album suffix stripping, dry-run methodology), unvalidated decisions (parser optimization, full library consistency)
- Rule system effectiveness: improvement loop broken (learnings captured in narrative but not converted to rules), TDD gate missing (2% test commits vs 30% expected), scope creep visible (82 IDEAS updates untracked mid-session)
- Recommendations: P0 (fix improvement loop in /dev-session), P1 (add TDD gate), P2 (add scope gate), P3+ (workspace cleanup and hardening)
- Impact: Identifies systematic improvement loop gap (narration without action defeats self-improvement). Sets priority for /dev-session skill enhancements (add enforced checkpoints for TDD, scope, rule capture)

**Integration Outcome:** Real integration attempt May 3 succeeded after blocker fixes applied May 5. 5531 songs verified clean post-integration. Decision logging enabled forensic analysis. Dry-run post-fix verified no regressions.

---

## 2026-05-05 (evening) - Scott Adams artist casing rule restored

**Blocker resolution:** Scott Adams title-casing rule was accidentally lost during the initial refactor to add `artist-name-overrides.xml` system. Rule existed in code comments but was not ported to the new config. Added `<Artist canonical="Scott Adams" />` to config/artist-name-overrides.xml. Clarified config file comment to document that override entries apply to both primary and featured (secondary) artists - one entry per artist covers all positions.

**Lesson:** When refactoring code that handles rules/config/overrides, audit existing rules BEFORE refactoring and ensure ALL are migrated to the new system. Silent data loss is serious. Feedback file created: `feedback_audit_rules_before_refactoring.md`.

---

## 2026-05-05 - TIER 1 complete: crash fix, TagFixer casing, LibChecker clean, first successful dry run

Three blockers from the 2026-05-03 partial integration crash resolved. First dry run post-fix completed with no errors.

**Blocker A - Integration crash: illegal characters in path**

`GetDestDir()`, `CountAlbumSongs()`, and `CountAkiraTheDonPersonAlbumSongs()` used raw `track.Album` values directly in `Path.Combine()` calls. Album tags can contain Windows-illegal characters (e.g. "WHAT IF?" contains `?`). Added `SanitiseFolderName()` helper in `MusicIntegrator` wrapping `Reflector.SanitiseFilename()`. Updated all four path-construction sites. Display strings (reason lines) still show the raw human-readable album name unchanged.

**Blocker B1 - Eels "soundtrack" LibChecker false positive**

Album "Useless Trinkets: B-sides, Soundtracks, Rarities and Unreleased" legitimately contains "Soundtracks". Added exception to `libchecker-exceptions.xml` matching `Artists contains "Eels"` + `Album contains "Useless Trinkets"`.

**Blocker B2 - Mike. single-song routing**

Confirmed not a routing logic bug - library already had 6 songs in `mike\the highs\` by the time the session ran. The failure was a transient artifact of the crash-interrupted partial run. Resolved once remaining songs were integrated.

**Bonus: TagFixer was title-casing "mike." to "Mike."**

`ExtractAndFixArtists()` applied `ToTitleCase(a.ToLower())` to all artists, silently converting "mike." to "Mike." on integration. This caused `CheckAlbumSubfolderRule()` to see only 1 song in the "Mike." group (the others were "mike."), falsely triggering the singles/album mismatch rule. Fix: added `config/artist-name-overrides.xml` with a `<Artist canonical="mike." />` entry and updated `ExtractAndFixArtists()` to use canonical casing from config instead of title-casing for listed artists. Two already-integrated "Mike." library files fixed manually via Mp3tag. Future integrations preserve "mike." correctly.

**Note on artist casing rules:** The override system (`artist-name-overrides.xml`) is the durable home for all artist casing rules going forward. During this refactor, some existing rules may have been missed - audit the codebase and HISTORY for any other artists that require non-standard casing (e.g. Scott Adams) and ensure they're migrated to config.

---

## 2026-05-03 - UX: Duplicate detection and output grouping (two interrelated items)

**Item A - Smarter [L] recommendation for ATD-style compilations**

`IsAlbumFolderCompilation()` previously collected only the primary artist from each XML, so ATD compilation albums (e.g. MEANINGWAVE MASTERPIECES V) were never detected - every track shares "Akira The Don" as primary. Changed to collect ALL artists from each XML (every semicolon-separated value), so the set now includes both ATD and the sampled person per track. For MEANINGWAVE V: ATD + 10+ different persons = 11+ distinct artists, easily crosses the threshold of 3. Traditional compilations (many different primary artists) still caught. The `libraryIsCompilation` flag in `BuildDupData()` was already wired to call `IsAlbumFolderCompilation()` - only the detection logic inside needed fixing. Updated dupReason text to match IDEAS.md spec.

**Item B - Consolidated duplicate execution outputs grouped before routing**

Step 3 (routing) previously executed D/L decisions inline within the routing loop, interleaving duplicate outcome messages with routing messages. User had to context-switch repeatedly. Refactored into two sub-passes: 3a iterates duplicate files and executes all D/L decisions (grouped output); 3b iterates all files and routes non-D/non-dry-run-L files. K files produce no output in 3a and fall through to routing in 3b. Real-mode L files have their library deletion executed in 3a (visible before routing begins) then are routed in 3b. Added `SkipRouting` flag to `DupData` to communicate 3a -> 3b. Both items compile and build clean.

---

## 2026-05-02 - Fix: plural "songs" when count is 1

Two reason strings in `MusicIntegrator.GetDestDir()` could produce "1 songs": the ATD Singles branch (`personSongCount < 3`, can be 1 or 2) and the Artists Singles branch (`albumCount < 2`, already had a `song(s)` workaround). Both fixed with inline ternary. All other count strings in the same method are guarded by `>= 2` or `>= 3` checks and were already correct.

---

## 2026-05-02 - UX: Add succinct routing summary line

Added `-> Artist / Folder` summary line above the full `Proposed:` path in the routing display block. `GetRouteSummary()` strips the top-level category folder (Artists, Musivation, etc.) and filename, then formats the remaining path as `A / B / C`. Special case: "Miscellaneous Songs" abbreviates to "Misc". User can scan the arrow line for quick Y/N; full path stays for diagnostics.

---

## 2026-05-02 - UX: Separator bars widened from 60 to 75 chars

`====` and `----` bars in MusicIntegrator.cs and TagFixer.cs extended from 60 to 75 characters. Long song titles (e.g. "WHAT YOU ARE LOOKING FOR IS WHAT YOU ARE") were overflowing the 60-char bars. Both bar types updated consistently via replace_all.

---

## 2026-05-02 - UX: Add Track + Album lines under In AudioMirror entry

Added `Track:` and `Album:` lines below the "In AudioMirror:" path in the duplicate detection block. Previously the library entry showed only the XML path; user had to parse folder segments to find the album. Now `ReadMirrorTrackInfo(xmlPath)` reads the XML directly and surfaces the same fields shown in the new file block. Both blocks now have matching information density. Falls back gracefully (lines omitted) if XML is unreadable.

---

## 2026-05-02 - UX: Remove redundant Track line + Separate Proposed from Reason

Two duplicate detection display cleanups in `MusicIntegrator.cs`.

**Remove redundant Track line:** The `Track: Artist - Title` line in the new file block was a repeat of what the `New file: Artist - Title.mp3` filename already showed. Removed the Track line. Album remains (it adds context the filename doesn't have).

**Separate Proposed from Reason:** `dupProposed` was bleeding justification into the action statement (e.g. "Delete NewMusic copy - already have this from 'Album'"). Now `Proposed` is a pure action ("Delete NewMusic copy, keep library" / "Delete library copy, keep new file" / "No version preference") and `Reason` carries the full justification. For the same-album case, the album name moved from Proposed into the Reason string so no context is lost.

---

## 2026-05-02 - UX: Same-song/same-album duplicate detection

When the new file is from the same album as the library copy, `MusicIntegrator` now detects this and immediately recommends [D] (keep library, delete NewMusic copy). Detection: `Path.GetFileName(Path.GetDirectoryName(duplicatePath))` gives the library album folder name; compared case-insensitively against `track.Album`. `dupProposed` shows the specific album name ("Delete NewMusic copy - already have this from 'Album'"). Takes priority over all other recommendation rules (single vs album, compilation vs album).

---

## 2026-05-02 - UX: Duplicate detection display overhaul (4 items)

Reworked the duplicate detection display block in `MusicIntegrator.cs` to match routing display quality.

**Layout (item 4):** Library context (AudioMirror path) comes first, then a blank line, then new file info as a grouped unit. User no longer has to mentally separate scattered fields.

**Corrected filename (item 5):** New file now shows the cleaned tag name (`Artist - Title.mp3`) instead of the raw original filename. Tags are already cleaned by TagFixer (real mode) or the dry-run simulation at this point, so this shows what will actually be stored.

**Proposed/Reason (item 2):** Added `Proposed:` line describing the recommended action (e.g. "Delete library copy, keep new file (album version preferred)") and `Reason:` line showing the rule that triggered it. Matches the context quality of routing proposals.

**Separator above options (item 3):** Added `----` separator line above the options line. Routing display always has `----` before and after options; duplicate display now matches.

---

## 2026-05-02 - Feat: Album preference rules in duplicate detection + DecisionLog logging

Extended duplicate detection with two album preference rules and decision logging.

**Rule 1 (existing, formalized):** Library has single + new file has real album -> recommend [L] (replace with album version). Was already in code but undocumented. Now has explicit reason string: "Library has single; new file is from album X - album preferred".

**Rule 2 (new):** Library has compilation track + new file has artist album -> recommend [L]. Detected via mirror path: if `relMirrorPath.StartsWith("Compilations\")`, the library copy is from a compilation. Artist albums are the definitive release. Example: library has "Changes" from a Various Artists compilation; new file is from 2Pac's Greatest Hits - recommend replace.

**newIsAlbum detection tightened:** Added `!track.Album.Equals("Missing")` guard to prevent false positives on tracks with no album tag.

**DecisionLog logging:** Duplicate decisions (D and L choices) are now logged to DecisionLog XML with reason. D logs "User kept library copy" (plus note if overriding a recommendation). L logs the preference rule reason, or "User chose to replace library copy" if no recommendation applied. K (keep both) is not separately logged - the downstream routing log entry is sufficient.

---

## 2026-05-02 - Fix: Dry-run now shows post-TagFixer (simulated) tags in routing blocks

Dry-run routing blocks were showing raw tags from disk, so `(feat. Artist)` would appear in the title, artist casing was uncorrected, and genre was unset - all misleading since real integration never sees these values (TagFixer cleans them first).

Fix: after reading raw tags in dry-run mode, MusicIntegrator now applies the same in-memory transformations as TagFixer before routing and display: `ExtractAndFixArtists` (feat. extraction + casing), `RemoveParentheticals` on title and album, genre assignment if needed. TCMP excluded (user trusts it). The simulation uses the original title for feat. extraction (correct order), then overwrites with cleaned values.

Result: dry-run is now a faithful preview of real integration. Routing proposals show the cleaned artist, cleaned title (no `feat.`), cleaned album, and correct genre - the same values that will be on disk when real integration runs. User can confirm tags and routing in one pass without cross-referencing the TagFixer output section.

Implementation: TagFixer helper methods (`RemoveParentheticals`, `ExtractAndFixArtists`, `ShouldFixGenre`, `DetermineGenre`) promoted from `private` to `internal static` so MusicIntegrator can call them directly.

---

## 2026-05-02 - Fix: ATD People/ routing now uses Singles/Album subfolder structure + artist casing fix

Two causally linked fixes applied together (ATD routing correctness depends on correct artist casing).

**Fix 1 - ATD People/ subfolder routing (TIER 0 BLOCKING):**
`GetDestDir()` was routing Akira The Don tracks directly to `People/{Person}/Song.mp3`, violating the Music-Library-Rules.md requirement for a subfolder before any song file. Fixed: when a person qualifies for their own People/ folder (3+ songs), the same album-vs-singles threshold logic used in the Artists/ path now also applies - if 2+ songs from the same album exist, routes to `People/{Person}/{Album}/`, otherwise routes to `People/{Person}/Singles/`. New private method `CountAkiraTheDonPersonAlbumSongs` counts songs for a given person+album combination (library + batch), mirroring the existing `CountAlbumSongs` method for the Artists/ path. Before this fix, all ATD tracks with 3+ person songs would land at wrong paths (missing the required subfolder level).

**Fix 2 - TagFixer compound artist name capitalization (TIER 1):**
`ExtractAndFixArtists()` was returning artist names without casing normalization. Example from real dry-run: "Akira The Don; Scott adams" where "adams" remained lowercase, producing wrong filenames and wrong People/ folder names in ATD routing (the two fixes are directly linked). Fix: added title-case normalization via `TextInfo.ToTitleCase(a.ToLower())` applied to each artist part independently before the `Distinct` call. Input "Akira The Don; Scott adams" now produces "Akira The Don; Scott Adams". Already-correct casing is preserved unchanged.

Both changes build cleanly.

---

## 2026-05-01 - Fix: Right-click in terminal no longer triggers Decline

Root cause: `Console.ReadKey(intercept: true)` immediately consumes any char arriving in stdin, including characters pasted by right-click in Windows Terminal. A clipboard containing "n" would silently fire the [N] Decline handler mid-routing - a data-safety bug.

Fix: replaced both `Console.ReadKey()` call sites in `MusicIntegrator.cs` with a new `ReadMenuKey()` helper that uses `Console.ReadLine()`. User must now type the key AND press Enter. Paste events appear visibly on screen and can be backspaced before submitting. Invalid input silently re-prompts with `> `.

Affected: duplicate resolution loop (D/L/K/Q) and routing confirmation loop (Y/N/Q).

---

## 2026-05-01 - UX: Blank lines between TagFixer output blocks

Added `Console.WriteLine()` after each `[WOULD FIX]` / `[FIXED]` block in the per-file results loop (`TagFixer.cs` lines ~192-203). Previously blocks printed back-to-back with no separation, making dry-run output hard to scan across 20+ files. One line change, confirmed working by user on real dry-run.

---

## 2026-04-29 Session 1 - UX: Remove folder picker, add Decline option

**UX FIX - Replace [N] Choose folder with [N] Decline:** Removed folder picker entirely. Previously, pressing [N] on routing proposal opened PickFolder() prompting user to type a destination path. This was "very bad" (user feedback) - most [N] presses mean "routing logic is wrong, not this specific folder." New flow: [N] simply declines file and leaves it in NewMusic for next run. Cleaner, faster, eliminates unnecessary user prompts. User can now say "no" without being forced to pick a folder.

**Code changes:**
- MusicIntegrator.cs line 293: Changed prompt from `[Y] Accept [N] Choose folder [Q] Quit` to `[Y] Accept [N] Decline [Q] Quit`
- Lines 341-354: Rewrote [N] handler to simply log decline and continue (removed 60+ lines of PickFolder call logic)
- Removed PickFolder() method entirely (was only called from [N] handler)
- Decision log now records declined files with status="declined" and reason="User declined routing" for audit trail

**UX ALREADY IMPLEMENTED - Relative paths in duplicate dialogs:** Item #2 (Shorten file paths in duplicate dialogs) was already implemented. Code analysis shows: duplicatePath from AudioMirror XML is converted to libraryFilePath via DeriveLibraryPathFromMirrorPath(), then shortened to relative path (lines 112-114) before display. Duplicate dialog shows `Musivation\Akira The Don\...` not full C:\Users\... path. No additional work needed.

**Result:** Routing proposals are now faster and less intrusive. User can accept, decline, or quit. No folder picker friction. Duplicate detection shows concise relative paths for easy comparison.

---

## 2026-04-28 Session 3 - UX polish: dry-run parity, Console.Clear removal, timestamped logging

**UX FIX - Dry-run mode now shows confirmation prompts:** Previously, dry-run would auto-accept all routes without prompting user. Now both dry-run and real mode show `[Y] Accept | [N] Choose folder | [Q] Quit` prompts for every file. Dry-run only differs in action: logs what would happen without moving files. Real mode actually moves files after user approval. This ensures user can validate routing decisions before running real integration.

**UX FIX - Console.Clear() removal:** Removed all 4 `Console.Clear()` calls from routing output that were wiping routing decisions from terminal. Replaced with blank lines (Console.WriteLine()) for visual separation. Users can now scroll back and see all routing decisions that were made during dry-run and real integration.

**UX ENHANCEMENT - Timestamped log entries:** Added timestamps to all routing decision outputs using new `PrintTimestamped()` helper method. Shows exact timing of each file processing (HH:mm:ss format). Reduces duplication and improves code maintainability - previously each output line had `$"[{DateTime.Now:HH:mm:ss}]"` repeated; now uses single helper. Timestamps logged to both console and file via TeeWriter.

**Code refactoring - PrintTimestamped() helper:** Created private method to avoid duplication of timestamp boilerplate. All routing output (proposals, confirmations, moves, errors) now calls PrintTimestamped() instead of repeating `$"[{DateTime.Now:HH:mm:ss}] {message}"`. Makes code cleaner and easier to maintain. Build verified - no compilation errors.

**Result:** All TIER 0 UX prerequisites complete. Ready for user to run real integration via launch.bat with full visibility and control.

---

## 2026-04-28 Session 2 - Routing logic fixes: album subfolder, Akira The Don detection, universal confirmation gates

**PRINCIPLE APPLIED - Universal confirmation gates (user control in early stages):** Changed real integration to require user confirmation for ALL routing decisions (not just Misc/ambiguous routes). Every file now prompts: `[Y] Accept | [N] Choose folder | [Q] Quit`. Gives user full control to verify routing, propose alternatives, or stop and review. This matches the principle: "give user more control in early stages of program, later on can automate when stabilised, heavily tested, etc." Dry-run mode unaffected (still just displays proposals).

**CRITICAL BUG FIX - Akira The Don artist name detection:** Fixed ATD not routing to Musivation in dry-run. Root cause: dry-run mode doesn't modify files, so genre=Musivation tag hasn't been set yet when MusicIntegrator reads for routing. Changed detection to check primary artist name directly (Akira The Don detection now happens before genre check), so it works in both dry-run and real mode.

**CRITICAL BUG FIX - Album subfolder logic (holistic counting):** Implemented correct album subfolder routing that counts songs holistically (library + new batch combined). Rule: if 2+ songs from album exist total, route to album folder; <2 songs -> Singles/ folder. Added `CountAlbumSongs()` method that scans both library (filesystem) and NewMusic (tag reading) to determine final album song count.

**CRITICAL BUG FIX - Akira The Don routing to Musivation/People subfolder:** Implemented special routing for Akira The Don tracks: genre=Musivation triggers detection of sampled person from secondary artist. Routes to `Musivation/Akira The Don/People/{person}/` if 3+ songs exist for that person (holistic count); otherwise -> `Musivation/Akira The Don/Singles/`. Added `CountAkiraTheDonPersonSongs()` helper that counts across library People folders, Singles, and NewMusic batch. Sampled person detection via secondary artist (after first semicolon in TPE1).

**ENHANCEMENT - 3+ song rule for Akira The Don People folders:** Implemented scan-ahead-style rule for Akira The Don sampled persons, matching the Artists folder logic: 3+ songs from same person creates dedicated folder, <3 songs go to Singles. This prevents scattered single songs while ensuring organizational consistency.

**VERIFICATION - Filename regeneration:** Confirmed TagFixer already handles filename regeneration correctly. Files are renamed during tag fixing step (`newFilename = {cleanedArtists} - {cleanedTitle}.mp3`), so filenames always match final tag values by the time integration routing begins.

**Documentation updated:**
- Music-Library-Rules.md: Updated Akira The Don section with 3+ person rule and holistic counting explanation
- Both rules now reference the holistic count principle (library + batch combined, not batch-only)

**Code changes:**
- MusicIntegrator.cs: Modified `GetDestDir()` to detect Akira The Don and handle People subfolder routing with 3+ count
- Added two helper methods: `CountAlbumSongs()` and `CountAkiraTheDonPersonSongs()` for holistic counting
- Build verified - no compilation errors

**Ready for testing:** All three routing logic bugs fixed. Now ready for user to run dry-run integration to verify routing decisions match expected patterns before real integration.

---

## 2026-04-28 Session 1 - Pre-integration fix suite: logging, skip error, Unicode, and separate tag fixing (TIER 0/1)

**P0 Bug Fix - Skip error crash:** Fixed `startIndex cannot be larger than length of string` exception in MusicIntegrator when processing skipped files. Added bounds checking to all Substring() calls that compute lengths from path operations.

**P1 Comprehensive Logging:** Extended TeeWriter to support dual console + file output. Wired up timestamped log files for all modes (analysis, integrate, tagfix) in `logs/` directory. All operations now logged to file automatically - users can scan log files for errors, patterns, and auditing after integration runs. Reused existing TeeWriter instead of creating duplicate LogWriter.

**P1 Unicode Fix:** Replaced non-ASCII Unicode rightwards arrows (→) with ASCII ` ->` in TagFixer output for proper console rendering. Fixed display issues showing delta character (␦) in tag comparison output.

**TIER 0 - Separate tag fixing from integration:** Confirmed TagFixer module runs as separate pre-integration step (line 53 of MusicIntegrator.cs). Integration assumes all input files have clean tags and focuses purely on routing decisions. Separation of concerns complete.

**TIER 1 - Auto-log routing decisions to XML:** Confirmed DecisionLog class implemented and wired into MusicIntegrator. Logs all routing decisions (auto-route, manual selection, etc.) to `decisions.xml` with full track metadata, destination path, routing reason, and dryRun flag. Decision logging at lines 268/287/319/368, saved at line 402. Ready for first real integration run with full audit trail.

---

## Settled / Not Doing

These were considered but explicitly deprioritized or deemed not worth implementing:

- **AudioMirror as primary scan target** - already implemented and correct. AudioMirror XML is the source of truth for all analysis and LibChecker runs. The actual audio files are never touched during analysis. Safer, faster, version-controlled. Any future analysis tools should read from AudioMirror XML, not audio files directly.
- **Full LibChecker unit test suite** - ROI not worth it (~400+ lines, 6-12 month payback). Add tests incrementally when rules change.
- **Full integration test with fake MP3s** - too heavy, dry-run already covers this.
- **Run-state tracking** - idea to track "did I already integrate this batch?" with state.json. Deprioritized because routing decisions.xml (TIER 1) provides batch-level summary implicitly (batch timestamp, file count, outcome via entry count).

---

## 2026-04-27 - TIER 0/TIER 3 features completed: TagFixer, LibChecker exceptions, markdown reports, README updates

Completed suite of safety and quality features spanning TIER 0 and TIER 3:

**TIER 0 - TagFixer module (pre-integration tag cleanup)**
- Created **TagFixer** command that runs before integration to clean raw NewMusic files
- Removes parenthetical phrases from Title/Album tags (e.g., `"Song (feat. Artist)"` -> `"Song"`)
- Renames files per convention: `{artists};{artists} - {title}.mp3`
- Moves featured artists to TPE1 tag (semicolon-separated)
- Sets TCMP=1 on all tracks
- Sets genre for Musivation/Motivation tracks
- Supports dry-run mode showing changes without modifying files
- Unblocks the first real integration run: user runs `tagfix` -> `integrate` -> `analyze` in sequence with zero manual tag work

**TIER 3 - Polish & documentation**
- Added `libchecker-exceptions.example.xml` template showing exception format and matching conditions
- Scripts folder structure verified: one-off Python scripts in `scripts/once-off/`; production launchers in `scripts/` root
- Dry-run coverage complete: TagFixer `--dry-run` shows tag changes, Integrator `--dry-run` shows routing decisions
- Markdown reports: ReportWriter and MusicIntegrator now output `.md` files with headers, bold, bullets, inline code
- Updated README: added folder picker mention to Integration Pipeline section and LibChecker Validations section listing all 11 checks

---

## 2026-04-27 - Constants.cs refactor and LibChecker Sources validation (TIER 1/3)

**Constants.cs refactor (TIER 3)** - Replaced all hardcoded `Path.GetFullPath(Path.Combine(BaseDirectory, "..", "..", ...))` chains with a single `FindRepoRoot()` helper that walks up the directory tree looking for sentinel files (CLAUDE.md, README.md, or .git). All paths now use: `Path.Combine(RepoRoot, "reports")` etc. Eliminates fragile depth-counting and self-heals if build output path changes. Commit: `3a3113e0`.

**LibChecker Sources OST validation (TIER 1)** - Implemented smart folder-to-album matching in `CheckSourcesFolder()`. Official soundtracks in Sources/Shows and Sources/Films are only flagged if album contains the source folder name (e.g., Peacemaker folder requires album "Peacemaker OST") but allows featured tracks to keep original album names (e.g., a-ha's "Hunting High and Low"). Eliminates 7 false positive flags and correctly distinguishes soundtrack-specific album tags from featured-track albums.

---

## 2026-04-27 - Pre-integration duplicate check and post-integration validation (TIER 0)

Implemented two safety features blocking TIER 0 to enable the first real integration run:

**Feature 1: Pre-integration duplicate check**
- Before routing each track from NewMusic, searches AudioMirror XML files for an existing track with the same primary artist AND title
- Comparison is case-insensitive and whitespace-trimmed
- If found: displays the matching library entry path and prompts user with [D]elete from NewMusic, [K]eep and continue, [Q]uit
- Dry-run mode shows what would be deleted without actually deleting
- Prevents accidentally importing songs already in the library

**Feature 2: Post-integration LibChecker auto-run**
- After integration completes successfully (not in dry-run), automatically regenerates AudioMirror and runs LibChecker on the updated library
- Catches broken integration runs immediately (zero issues expected if integration was clean)
- Reports "CLEAN" or "ISSUES FOUND" for user visibility
- Both features unblock the first real integration run by providing automated safety gates: duplicate prevention and post-integration validation

---

## 2026-04-27 - LibChecker stability: eliminated duplicate detection and false positive bugs

Two causally-linked LibChecker bugs fixed in one session, both blocking clean TIER 1 integration runs:

**Fix 1: Duplicate detection now considers all featured artists**
- Changed grouping from `{ Title, PrimaryArtist }` to `{ Title, Artists }`
- Previously: "Twista;CeeLo - Hope" and "Twista;Faith Evans - Hope" wrongly flagged as duplicates
- Now: Only flagged if title AND all artists match exactly
- Verified with full library analysis (5489 tracks) - no false positives

**Fix 2: Eliminated 'feat.' and 'ft.' false positives in titles/filenames**
- Changed unwanted-string detection from simple `Contains` to regex for abbreviations
- Previously flagged mid-word matches: "Identity Theft" (ft.), "NEVER LEFT" (ft.), "NO TEARS LEFT" (ft.)
- Now requires whitespace/punctuation before abbreviation: `(?:^|[\s\-\(\)\[\]\/,])`
- Legitimate featured artists like "(feat. Artist)" or " ft. Artist" still correctly flagged
- Verified: full library analysis, 0 false positives for "feat."/"ft."

Both fixes are in the same file (LibChecker.cs) and address the same outcome: enabling reliable TIER 1 integration runs. Committed separately but documented together as a subsystem stabilization.

---

## Previous entry (2026-04-27) - Fixed LibChecker duplicate detection to consider all featured artists

(Merged into the combined entry above.)

LibChecker's duplicate detection was grouping tracks by `{ Title, PrimaryArtist }` only, incorrectly flagging different songs with the same title but different featured artists as duplicates. Example: "Twista;CeeLo - Hope" and "Twista;Faith Evans - Hope" were wrongly grouped together despite having different collaborators.

Fixed by changing the grouping key from `PrimaryArtist` (first artist only) to the full `Artists` field (all collaborators). Now duplicates are only flagged when both title AND all featured artists match exactly. Verified with full library analysis (5489 tracks) - no false positives, and program correctly identifies true duplicates (of which there are currently zero).

This unblocks TIER 1 integration runs, which depend on reliable duplicate detection to prevent importing songs already in the library.

---

## 2026-04-27 - Fixed build script for VS2026 (MSBuild path update)

Visual Studio 2022 Community updated to the 2026 version (VS 18). The old MSBuild path `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe` was no longer valid. Updated `scripts/build.bat` to use the new path: `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`. Build now succeeds with MSBuild 18.5.4. Verified by running build script end-to-end - AudioManager.exe compiles cleanly.

---

## 2026-04-26 - Pre-integration gate: verify AudioMirror fresh and LibChecker clean

Implemented mandatory safety gate before integration runs. When user invokes `integrate` mode:
1. Regenerates AudioMirror XMLs to ensure current library state is captured
2. Checks if XMLs changed (ignores LastRunInfo.txt timestamp-only updates) - if XMLs changed, mirror is stale
3. Runs LibChecker on the fresh data - if any issues found, must be fixed first
4. Only proceeds to integration if BOTH checks pass: fresh mirror AND clean library

Error messages guide user: "AudioMirror is out of sync with the library" or "LibChecker found issues in the library. Fix library issues before adding new songs."

Prevents corruption risk by ensuring new songs are integrated only when library state is known-good. Tested with real library (5491 tracks) - gate correctly rejects integration when LibChecker finds issues, allows proceed when both conditions met.

---

## 2026-04-25 - Phase 0 complete: program verified runnable end-to-end

All Phase 0 blockers resolved. MSBuild compiles clean. `integrate --dry-run` handles empty inbox correctly. `analysis` runs end-to-end: mirror regenerated (5491 tracks), metadata parsed, stats generated, LibChecker ran, report saved, AudioMirror commit correctly blocked due to LibChecker hits.

- `launch.bat` platform flag fixed (`x86` -> `Any CPU`) - done 2026-04-10
- `AudioMirrorCommitter.cs` registered in csproj - done 2026-04-10
- CRITICAL csproj file-registration note added to CLAUDE.md Build and Run section - done 2026-04-10
- Full end-to-end smoke test (MSBuild + all modes) verified by Claude - done 2026-04-25

---

## 2026-04-10 - Constants.MirrorRepoPath made absolute (cwd-independent)

`Constants.MirrorRepoPath` was built from the relative `ProjectPath = "..\\..\\..\\"` literal, which .NET resolves against the **current working directory**, not the exe location. When launched via `scripts/launch.bat` (cwd = `scripts\`), the relative path walked up too far and resolved to `C:\AudioMirror\` - producing `Could not find path 'C:\AudioMirror\LastRunInfo.txt'` in `AgeChecker..ctor` before anything else could run. Fixed by rebuilding `MirrorRepoPath` with `Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "AudioMirror"))` - same pattern already used by `ReportsPath`, `LibCheckerExceptionsPath`, and `LogsPath`. Also tidied `MirrorFolderPath` to use `Path.Combine` (removes a stray double-backslash). Callers that already wrapped `MirrorRepoPath` in `Path.GetFullPath(Path.Combine(...))` continue to work (idempotent). Verified by running `AudioManager.exe analysis` from a `scripts\` cwd - mirror path now resolves to `C:\Users\David\GitHubRepos\AudioMirror\AUDIO_MIRROR` and the full analysis pipeline runs clean.

---

## 2026-04-10 - Build fixes: launch.bat platform + missing csproj Compile entry

Two build failures surfaced when trying to run LibChecker for the first time since the integration pipeline work:

1. **`scripts/launch.bat` line 19** passed `-p:Platform=x86` to MSBuild, but the solution only defines `Debug|Any CPU` and `Release|Any CPU`. Error: `MSB4126: The specified solution configuration "Release|x86" is invalid`. Fixed to `-p:Platform="Any CPU"`. CLAUDE.md line 52 had the same bad example and was also corrected, so future sessions don't keep copying it.

2. **`AudioMirrorCommitter.cs` was never registered in `AudioManager.csproj`.** The file was created in an earlier session but the old-style .NET Framework csproj requires an explicit `<Compile Include="..." />` entry per file - new files are NOT auto-included. Error: `CS0103: The name 'AudioMirrorCommitter' does not exist in the current context`. Added the Compile entry. Added a CRITICAL guard rule to CLAUDE.md "Build and Run" section so this can't silently recur - any new .cs file must be registered in the csproj.

---

## 2026-04-09 - LibChecker: album subfolder rule + inverse genre check

Two new checks added based on Music-Library-Rules.md gap analysis:

- `CheckAlbumSubfolderRule()`: flags Artists/ tracks violating the album subfolder rule.
  - 2+ songs from same album in `Singles/` instead of an album subfolder.
  - Only 1 song from an album in an album-named subfolder instead of `Singles/`.
- `CheckGenreVsFolder(folder, genre)`: flags tracks with Musivation/Motivation genre that are
  outside their expected folder (inverse of the existing `CheckMainFolderForGenre` check).
- `Constants.SinglesDir = "Singles"` added.

---

## 2026-04-09 - LibChecker exceptions moved to config file

`IsExceptionToRules()` hardcoded whitelists extracted to `config/libchecker-exceptions.xml`. LibChecker now loads exceptions at startup via `LoadExceptions()`. Add new exceptions without recompiling - edit the XML and run. Existing exceptions: KRS-One, Original Rappers, Going To Be Alright, Medicine Man, Edition albums, Soundtrack 2 My Life, Agatha All Along, Eric Thomas bonus interview.

---

## 2026-04-09 - AudioMirror auto-commit, scan-ahead routing, LibChecker IsClean

- `AudioMirrorCommitter.TryCommit()`: after every analysis run, if LibChecker clean and AUDIO_MIRROR changed, auto-commits with "MMM d Update" message and pushes. Skips if issues found or nothing changed.
- `LibChecker.IsClean`: new property. `grandTotalHits` accumulates across all check methods via `PrintTotalHits()`. Prints "LibChecker: Clean" when zero hits. Program.cs detects this from captured output.
- `RunScanAhead()`: pre-scans batch MP3 tags + AudioMirror Misc XMLs. Artists hitting 3+ threshold get routed to `Artists/{artist}/` instead of Misc. Preview printed before per-file loop. Existing Misc songs for those artists flagged for manual migration (not auto-moved - risky).
- `GetDestDir()`: scan-ahead HashSet passed in. Routes scan-ahead artists to Artists/ with "[new via scan-ahead]" reason note.

---

## 2026-04-09 - Integration pipeline: pre-process tags, log, routing fix

- `PreProcessTags()` runs on every incoming file before routing: sets TCMP=True on all tracks, sets Genre=Musivation for Akira The Don. Works in dry-run mode (prints what would change without writing).
- `SaveLog()` writes `logs/integration-YYYYMMDD[-dryrun].txt` after every run. Per-file: filename, artists, title, album, destination, tag changes, status. Summary line with totals.
- `GetDestDir()`: fixed routing bug - when artist folder exists but no distinct album, routes to `Singles/` instead of artist root (prevents loose files that LibChecker would flag).
- TagLib resource leak fixed: `TagLib.File.Create()` now wrapped in `using` block.
- `SourcesDir = "Sources"` added to Constants (was the only main folder without one).
- `CheckSourcesFolder()` added to LibChecker: flags Sources/Films and Sources/Shows tracks where Album does not contain "OST".
- Analyser library size: fixed to count only `.mp3` files (previously included .ini, .lnk, etc.).
- `LogsPath` added to Constants (gitignored `logs/` folder).

---

## 2026-04-09 - Batch launcher, CLI args, dry-run mode

- `scripts/launch.bat` - menu-driven launcher, auto-builds via MSBuild, 4 modes (analysis / force-regen / dry-run / real integrate), `cmd /k` to keep window open.
- `Program.cs` - CLI args support: `AudioManager.exe analysis [--force-regen]` or `AudioManager.exe integrate [--dry-run]`. Interactive menu still works when no args passed.
- `MusicIntegrator` - `--dry-run` flag: prints all planned moves without executing any file operations. Shows `[DRY RUN]` prefix. Summary says "Would move" instead of "Moved".

---

## 2026-04-08 - Docs & Knowledge tasks complete

- **Batch A date fixed** - `NewMusic-Integration-Plan-20260407.md` renamed to `20260308`; AudioMirror commit `b8e15b1` confirmed the integration was March 8 2026, not April 7.
- **Music-Library-Rules.md expanded** - Added Sources folder rules (Films, Shows, Anime), full workflow (Stages 1-3), Compilations folder, MP3 conversion, "no album -> use title" rule, Miscellaneous review guidance. Sources folder was completely missing before.
- **Word doc retired as source of truth** - All content from `Audio Folder Organisation Usage Process.docx` extracted into `Music-Library-Rules.md`.
- **Integration plan review** - Both Batch A (20260308) and Batch B (20260407b) reviewed; no additional routing rules found that weren't already in `Music-Library-Rules.md` after the expansion.

---

## 2026-04-08 - Architecture decisions settled

**Integrator stays in AudioManager** - shares Constants, LibChecker, and tag models with the rest of AudioManager. Splitting would require duplicating or packaging shared code with no real gain at this scale. Logical separation (each component has its own entry point) is sufficient.

**Audio reports stay in AudioManager** - AudioMirror is a data repo; generating/storing analysis reports there would blur its purpose. AudioManager owns the tools and the outputs.

**LibChecker output in audio report** - "Checking library..." process lines do not belong in a stats report. Rule: if LibChecker clean, show a single `LibChecker: Clean` line in the report. If issues exist, prominently flag them in the report AND block the AudioMirror commit - fix issues first, then commit both the report and AudioMirror changes.

---

## 2026-04-08 - LibChecker: version/edition suffix detection

Added `"version"` and `"explicit"` to `Constants.UnwantedInfo`. LibChecker now flags titles containing `(Explicit Version)`, `(Album Version)` etc. `(Radio Edit)` and `(Deluxe Edition)` were already caught by the existing `"edit"` entry.

---

## Constants.cs consolidation (already done)

`MiscDir`, `ArtistsDir`, `MusivDir`, `MotivDir` were already defined in `Constants.cs`. Both `LibChecker` and `MusicIntegrator` already read from `Constants` - no duplication existed.

---

## LibChecker owns validation (already done)

`MusicIntegrator`'s only "validation" is a routing precondition (skip files missing artist/title - can't determine destination without them). This is not duplicated LibChecker logic. The boundary was already clean.

---

## ReportWriter - plain static class (already done)

`ReportWriter` is already `internal static class`, not inheriting from Doer. Idea retired.

---

## Analyser stats (already implemented)

Total playback hours, average and median song length, total library size (GB), average file size (MB), track age stats (average, median, oldest, newest), year and decade frequency distribution. All in `Analyser.cs`.

---

## 2026-04-07 - Fix git folder casing

Git tracked folders in old uppercase names (`PROJECT/`, `REPORTS/`, `Docs/`) while they were lowercase on disk. Fixed via two-step `git mv` per folder (required because Windows filesystem is case-insensitive and ignores direct renames). Also updated `REPORTS` path string in `Constants.cs` and comments in `ReportWriter.cs` to match.

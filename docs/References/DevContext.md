# AudioManager - Developer Context

Implementation invariants, architecture notes, and code patterns. Read this when working on routing, tag, or integration code. Not needed for analysis-only or doc sessions.

---

## AudioMirror as Classification Oracle

**The AudioMirror is the source of truth for facts about the library. Use it to answer classification questions - not name heuristics.**

When you need to determine what something IS (compilation? artist album? genre folder?), read the actual data in AudioMirror rather than pattern-matching names or paths.

**Examples:**
- **Is this album a compilation?** - Read all XML files in the album folder, collect the full `Artists` field from each track. If many distinct artists appear across tracks = compilation. If the same artist appears on every track = artist album. This works on any album name, including unusual ones like "MEANINGWAVE MASTERPIECES V" that keyword lists would miss.
- **Does an artist have a folder?** - Check `Directory.Exists(artistFolder)` against the library, not string matching.
- **Is this a Singles folder?** - Check the path component, not the album tag.

**Why this is better than heuristics:** Names drift; heuristics have edge cases; data doesn't lie. Before writing any keyword list, regex, or name-pattern check, ask: "Does the AudioMirror data already answer this question more reliably?" In most cases, it does. Read the XMLs.

---

## Stage 3 Integration Architecture

**User workflow:** See Music-Discovery-Workflow.md. This section explains how it works for developers.

**The pipeline has two conceptually separate concerns:**

1. **TagFixer (tag cleanup phase):** Reads raw NewMusic files, applies automatic corrections:
   - Removes unwanted words from Title/Album: "(feat. ...)", "(Album Version)", "(Explicit)", etc.
   - Ensures featured artists in TPE1 as semicolon-separated list
   - Renames files to `{artist} - {title}.mp3` format
   - Sets TCMP=1 (prevents iTunes album grouping)
   - Sets genre for Musivation/Motivation tracks per Music-Library-Rules.md

2. **Integration (routing phase):** Routes cleaned files to library destinations:
   - Applies rules from Music-Library-Rules.md (Artists folder, Compilations, Musivation, Motivation, Sources, Miscellaneous)
   - Respects 3-song threshold scan-ahead for album subfolder creation
   - All console output captured to `logs/run-YYYYMMDD-HHmmss.log` with `[HH:mm:ss]` timestamps (via TeeWriter in Program.cs)
   - Dry-run mode: previews all fixes + routing without moving files
   - Post-integration: auto-runs LibChecker to validate library clean

**Design rationale:** Separating tag-fixing from routing makes integration testable and reversible. TagFixer produces clean, ready-to-route files. Integration purely routes. Audit trail documents every decision. If routing fails, it's a routing bug, not a tag issue.

---

## ParseCache Architecture

Three-layer cache system. Understanding this prevents confusion when diagnosing stale analysis output.

```
MP3 files (ground truth - C:\Users\David\Audio\)
    ↓ Reflector refreshes (force regen = full rebuild; incremental = new files + stale XMLs refreshed)
AudioMirror XMLs (Layer 1 - metadata snapshot, mtime updated on write)
    ↓ Parser reads
ParseCache logs/parse-cache.txt (Layer 2 - parsed TrackTag objects, flat pipe-delimited file)
```

**`IsMirrorStale(mirrorPath, cacheTime)` returns true (cache miss) when:**
- No XML files found - mirror is empty (Reflector crashed after `Directory.Delete`, before recreating)
- Any XML `LastWriteTime > cacheTime` - new song added or force regen ran

Any filesystem error in the mtime check also degrades to a miss (never crashes).

**Reflector incremental behavior (Layer 1):**
- **Detects tag edits to existing MP3s (fixed 2026-06-02):** `IsStaleMirrorXml(mp3Path, xmlPath)` compares `MP3.LastWriteTimeUtc` vs `XML.LastWriteTimeUtc`. If MP3 is newer, overwrites XML with real path, triggering a fresh tag read on that analysis run. Counter shown in output only when > 0 (no noise on normal runs).
- **Does NOT remove XMLs for deleted MP3s:** Reflector only iterates real files and creates; never deletes orphaned XMLs. Force regen fully handles this (deletes mirror entirely, rebuilds). Won't-fix for incremental mode - the expected workflow for library deletions is always followed by force regen.

ParseCache inherits the second limitation - a deleted MP3's cached data persists until force regen. Stale XML entries (first limitation) no longer affect ParseCache after the 2026-06-02 fix.

**Force regen removes all staleness:** deletes mirror entirely, rebuilds all XMLs from MP3 reads, then Parser saves a fresh cache.

**Validation-after-deletion hazard (the ghost-XML trap):** the add-only nature of incremental regen is not just an analysis concern - integration itself deletes (`[L]` duplicate decisions) and moves (Misc migration) library files, then post-integration validation regenerates incrementally. The vacated locations keep their XMLs, so LibChecker validates a mirror that describes a library state that no longer exists and flags the ghosts as duplicates / misplaced songs. Fast tell: post-integration regen prints `Tags parsed > MP3 file count` - the difference is the ghost count (2026-06-28: 5702 parsed vs 5693 on disk = 9 ghosts = 9 vacated locations). Before treating any post-integration LibChecker warning as real, force-regen and re-check; warnings at a deleted/moved-from path are almost always ghosts. The durable fix is to make incremental regen prune orphaned XMLs (IDEAS TIER 1). Full analysis: `docs/References/Post-Integration-Validation.md`.

**Projection pattern for pre-move validation:** LibChecker consumes `List<TrackTag>`, never MP3 files - so the post-integration library state can be validated in memory during the dry run, before anything moves. Build a projected tag list: current parsed tags, minus the TrackTags the integrator will delete or move away from, plus synthesized destination-path TrackTags carrying the post-TagFixer values (the integrator already holds these). Running LibChecker on the projection predicts real issues with zero ghost false positives, because deletions are modelled by construction. This is the preferred validation seam (IDEAS TIER 1) - any new check that needs "what will the library look like after this batch?" should build a projected `List<TrackTag>` rather than inspecting the live mirror after the fact.

---

## Code Invariants

- **Routing-LibChecker threshold parity.** When a new routing destination is added to `GetDestDir()` (e.g. Compilations/ with "3+ distinct artists" threshold), the LibChecker rule that validates that destination MUST use the same detection threshold. Mismatch = routing<->LibChecker divergence: tracks correctly routed get falsely flagged. Discovered 2026-06-03: `CheckCompilationsFolder` was flagging genuine compilations because it lacked the 3+ distinct artist check that `RunScanAhead` uses.

- **ParseCache is tightly coupled to TrackTag field count.** The 12-param cache constructor in `TrackTag` and the ParseCache serialize/deserialize format must stay in sync. Any Track.cs schema change (add/remove fields, e.g. Phase 2.5 cover art redesign) requires updating the cache constructor and `ParseCache.cs` field count. The cache auto-invalidates on any new/modified XML, so stale data is never silently served - but a field count mismatch will cause `TryDeserialize` to return false on every read (always cache miss) until fixed.

- **Dry-run parity for ALL integration operations** - every step that moves or deletes files must have a dry-run branch that prints what would happen instead. `RunMiscMigration` checks `dryRun` and prints `[DRY RUN] Would move:`. Never add a new integration operation without implementing the dry-run counterpart first.

- **TeeWriter.WriteCharToFile** is the single source of truth for file timestamp logic. `Write(char)` and `WriteLine(string)` both delegate to it. Any TeeWriter change must preserve this. `WriteLine(string)` checks for embedded `\n` and processes char-by-char via `WriteCharToFile` - do not revert to the old single-string write path.

- **RoutingConfidence enum removed 2026-05-31.** `GetDestDir()` now returns `out bool isNewFolder` (true = scan-ahead is creating a new Artists/ folder for this track). Display: `[AUTO - new folder]` vs `[AUTO]`. Do not re-add the enum; the `Uncertain` path was dead since 2026-05-25.

- **LibChecker.CheckArtistFolder() uses case-sensitive String.Equals** (line 373). Windows NTFS case-insensitivity protects file routing but NOT LibChecker validation. Any artist with non-standard casing that gets a new track integrated will cause a LibChecker flag even if the file lands in the right folder. Protection: artist-name-overrides.xml (comprehensive as of 2026-05-26, audited post-May integration run).

- **DetermineGenre(artists)** is only called when `ShouldFixGenre` returned true. The else-branch returning `Constants.MotivDir` is intentional - Motivation normalization is the only non-Musivation trigger path.

- **GetRouteCategory(string destDir)** - private static in MusicIntegrator. Strips AudioFolderPath prefix, returns first folder component. Maps "Miscellaneous Songs" -> "Misc". Used by dry-run distribution summary.

- **RunScanAhead must check ALL routing destinations, not just Artists/.** Artists homed in Musivation/ (e.g. Akira The Don) have no Artists/{name}/ folder - scan-ahead used to falsely flag them as needing a new Artists/ folder. Fix (2026-05-26): also check Directory.Exists(Musivation/{artist}/). Any future alternative routing home must be added to this check or it will generate wrong scan-ahead messages and incorrect Misc migration paths.

- **BuildMirrorIndex() is the pattern for any batch-vs-library comparison.** Pre-load all AudioMirror XMLs into Dictionary<string,string> (normalised "primary\0title" -> xmlPath) once before the loop. O(mirror) setup, O(1) per-file lookup. Never do a full Directory.GetFiles scan inside a per-file loop - that's O(mirror x batch) and caused a ~1min silent hang on a 5531-track library with 126 batch files. Uses `Parallel.ForEach` + `ConcurrentDictionary` (2026-06-27): `XmlDocument` is local per iteration, alias dicts are read-only, safe to parallelize.

- **`_batchTagCache` is populated by RunScanAhead and consumed by PreScanFiles** (2026-06-27). Stores raw tag data from the first TagLib pass so PreScanFiles skips its own read. Two invariants: (1) cache entries use raw (un-normalized) values - PreScanFiles does its own normalization; (2) the fallback path `TagLib.File.Create` must stay for files that RunScanAhead skipped due to read errors. In test constructors: initialize as `new Dictionary<...>()` not null (same rule as other scan-ahead fields).

- **`NormaliseTitleForDedup()` is applied on BOTH sides of every dedup key** (2026-06-27). Applied in `GetAliasExpandedKeys` (index build) and `FindDuplicateInMirror` (lookup). Strips `(feat./ft./featuring X)` only - not (Remix), (Edit), (Live) which are distinct tracks. Rationale: library tags may predate TagFixer cleanup; incoming batch has already been cleaned; without normalization, "Song (feat. X)" in library silently fails to match "Song" in batch.

- **RunScanAhead counts batch + Misc + Sources/ for artist folder threshold.** Sources/ tracks are scanned via `Path.Combine(Constants.MirrorFolderPath, Constants.SourcesDir)` with `SearchOption.AllDirectories` - same XML parsing as Misc. Sources tracks count toward the 3-song threshold but are NOT added to `_miscMigrationCandidates` - they stay in Sources/ regardless of whether the artist gets promoted. (Fix 2026-05-26: without Sources/, Common with 1 Sources/Films song + 2 batch songs still routed to Misc.)

- **Dry-run "Skipped" count is not the real-mode "Skipped" count.** In dry-run, L-decided duplicates have `SkipRouting = true` (MusicIntegrator.cs ~line 200) and count as Skipped - they cannot be routed because the library copy hasn't been deleted yet. In real mode, `SkipRouting` stays false for L decisions, so those files ARE routed in step 3b. Never treat dry-run "Skipped: N" as a data loss signal without checking which decision type caused it.

- **`SanitiseFilename` uses Cyrillic encoding best-fit to map diacritics to ASCII** (Reflector.cs line 289: `Encoding.ASCII.GetString(Encoding.GetEncoding("Cyrillic").GetBytes(...))`). Example: `ÿ` (U+00FF) -> `y`. Any artist tag with diacritics that survives `ExtractAndFixArtists` unchanged will produce a tag/filename mismatch and trigger LibChecker `CheckFilenamesCasingsMatchArtistTags`. Solution: add the diacritic form as a `variant` in artist-name-overrides.xml so it normalizes to canonical before dedup.

- **`artist-name-overrides.xml` supports a `variant` attribute** (added 2026-06-27) that maps Unicode-stylized names to their canonical form (e.g. `variant="JAŸ-Z"` on the Jay-Z entry). If a streaming provider encodes an artist name with diacritics or stylized Unicode, add a `variant` entry rather than changing code. Multiple variants per entry are not supported - add a new `<Artist>` element if needed.

- **`_artistOverrides` static field in TagFixer is populated once per process** from the config file on first `GetArtistOverrides()` call. Safe to change the config file between test runs (each verify.bat invocation starts a fresh process), but changes won't take effect mid-process.

- **RunScanAhead detects compilation albums (added 2026-06-02):** In the same TagLib# loop that builds `batchCounts`, it maps `album → HashSet<primaryArtist>`. Albums with 3+ distinct primary artists are stored in `_compilationAlbums`. GetDestDir receives `compilationAlbums` as an explicit parameter (like `newArtistFolders`) and routes qualifying tracks to `Compilations/{album}/` when the primary artist has no Artists/ folder. Tests pass `compilationAlbums` directly via the updated `GetDestDir` signature.

- **ExtractAndFixArtists: `" & "` in title parentheticals is always a collaborator separator, never part of an artist name.** The code splits on `" & "` before the duplicate-artist check and adds each component individually. This invariant must be preserved if featured-artist extraction is ever refactored - never add the compound "A & B" form when A and B will both be present separately. (Fix 2026-05-26: Perry Como track was producing 4 artist entries instead of 3.)

- **ManifestRunner test pattern:** `new MusicIntegrator(Constants.AudioFolderPath)` (the test constructor with real library path) lets you call `GetDestDir()` against the real folder structure without triggering the integration pipeline. Use for any future routing regression test that needs the real library but no NewMusic files.

- **Album tag inheritance in PreScanFiles:** When album tag is "Missing" and the file is in an immediate subfolder of NewMusic, the subfolder name is used as the album fallback. This runs BEFORE TagFixer simulation, so suffix stripping applies to inherited album names too. Location: `PreScanFiles()` in MusicIntegrator, right before the `if (dryRun && ...)` block.

- **CommitTrigger enum in AudioMirrorCommitter:** AnalysisForceRegen and Integration trigger auto-commit (mirror is reliable); AnalysisIncremental skips silently (stale XMLs possible). Never pushes - user pushes manually. Safety gate: LibChecker must be clean AND files must have changed.

- **`AgeChecker.ForceRegen(lastRunInfoPath=null)` is the correct seam for forcing a full mirror rebuild.** Sets `RegenMirror=true` AND writes `LastRunInfo.txt` with current timestamp (so the audit file is committed alongside the mirror). Use this over direct `AgeChecker.RegenMirror = true` assignment - the latter skips the LastRunInfo update. Post-integration flow calls this before `new Reflector()` so orphaned XMLs from deleted/moved library files are pruned.

- **TagLib#'s `JoinedPerformers` uses `"; "` (semicolon space) as the join separator** for Performers arrays. TagFixer's `string.Join(";", artistList)` uses no space. These diverge for files where Performers is stored as an array - `artistsChanged` detects a difference even when artists are already correct. Any future artist-equality check should either compare the Performers array directly, or normalize both sides to the same separator before string comparison. See TIER 3 item "TagFixer artist separator diverges from TagLib# JoinedPerformers format".

- **verify.bat runs full suite:** build + unit tests + manifest tests. Always pass `--no-pause` when calling from Claude. All assertions must be green before any C# commit.

- **`StripAlbumSuffixes` is the single canonical album normalizer.** Called by TagFixer (line 97), RunScanAhead (line 463), and PreScanFiles dry-run path (line 1286). When adding new version-suffix patterns (`(Mono)`, `(Stereo)`, `(YYYY Remastered Edition)`, etc.), edit `StripAlbumSuffixes` only - all three callers benefit automatically. Never inline suffix-stripping logic in scan-ahead or GetDestDir.

- **`FindAlbumFolder(artistFolder, cleanAlbum)` is the safe album folder lookup.** Returns the exact-match folder if it exists, then falls back to a fuzzy match (strips version suffixes from each subfolder name and compares). Use it instead of `Path.Combine(artistFolder, SanitiseFolderName(album))` anywhere an album folder is looked up in the library (`CountAlbumSongs`, `GetDestDir`). Direct construction misses the case where the on-disk folder has a suffix the incoming tag doesn't (e.g. `"Life After Death (2014 Remastered Edition)/"` vs clean tag `"Life After Death"`).

- **Test deactivation pattern (reflection-discovered tests):** TestRunner discovers tests via `GetMethods(BindingFlags.Static | BindingFlags.Public)`. To deactivate a test without deleting it (e.g. when a LibChecker check is deferred until library remediation), change `public static void` to `private static void`. Add `_DEFERRED` suffix to the method name as a signal. Re-enable: revert to public + restore the production code it exercises. The `_IsClean` counterpart tests should remain public - they verify the non-blocking path still passes.

- **Deferred LibChecker check pattern:** When a new LibChecker check would block integration due to pre-existing library violations, keep the code as a commented-out block in `CheckFilename()` with a clear pointer to the remediation task in IDEAS.md. The `_IsDirty` test goes private (`_DEFERRED`); the `_IsClean` tests stay active. Re-enable both together once remediation is complete. Current deferred check: multi-artist semicolon delimiter validation (~436 files pending Mp3tag bulk-rename - IDEAS.md TIER 1).

- **Parser parallel reads: order of `audioTags` list is non-deterministic** (2026-06-27). `Parallel.ForEach` into `ConcurrentBag` gives no ordering guarantee. All consumers (LibChecker, Analyser, ParseCache) iterate the full list without order dependency - this is fine. Do NOT add any code that relies on `audioTags` order matching mirror folder traversal order.

---

## Repo Patterns

- **TrackXML : Track is known design debt (TIER 2 in IDEAS.md).** Do not model new serialization code on this inheritance. Any new XML field should wait for the XML refactor item. The target design is `internal static class TrackXML` with `Read(path, TrackTag)` and `Write(path, TrackTag)` using `XDocument` (System.Xml.Linq). `XDocument` is strictly cleaner: `r.Element("Title")?.Value ?? ""` vs XPath strings, and the write path is one `new XDocument(new XElement("Track", ...))` expression.

- **ParseCache Extract() pattern (TIER 2 prerequisite).** When the XML refactor ships, replace `private const int FieldCount = 12` with a static `string[] Extract(TrackTag t)` method returning all fields in order. `FieldCount` becomes `Extract(null).Length`. Save uses `string.Join(Sep, Extract(t))`. Deserialize validates `parts.Length == FieldCount`. One edit to add a field, no sync risk.

- **artist-name-overrides.xml is loaded at runtime** (XmlDocument.Load at first call, cached per process). No rebuild needed when adding entries - changes take effect on next exe run.

- **Colons in ID3 album tags become underscores in folder/filenames** via Path.GetInvalidFileNameChars() in SanitiseFilename. The ID3 tag itself retains the colon. Expected behavior, not a bug.

- **TagFixer artist field mutations must be idempotent.** Before appending a normalized artist entry, check if the normalized form is already a substring of any existing artist entry - skip if present. Bug: compound form "X & Y" in the field + individual "X" and "Y" added = duplicates.

- **Sort routing display output by destination path before printing**, not by file processing order. Tracks from the same artist/album should be grouped together in dry-run output so routing anomalies are visible by clustering.

- **Test constructor pattern for MusicIntegrator:** `internal MusicIntegrator(string testLibraryPath)` bypasses the full pipeline, sets `_libraryPath` and initializes empty scan-ahead dicts. Use for any new module that can't be instantiated cheaply via the public constructor.

- **RoutingFixtures.AddAlbumFiles uses `new byte[0]` placeholder .mp3 files.** Safe because `CountAlbumSongs` only calls `Directory.GetFiles(*.mp3)` on the library side - it never opens them with TagLib#. Do NOT use this trick for test scenarios that trigger TagLib# reads (e.g. the NewMusic batch scan path in CountAlbumSongs).

- **LibChecker.CheckFilenameForStr is case-sensitive** (plain `.Contains()`, no `StringComparison.OrdinalIgnoreCase`). Any MP3 filename with different casing than the ID3 artist tag will produce a "should include" warning. `Reflector.SanitiseFilename` and `StandardiseStr` never change case - they only remove invalid chars, control chars, and underscores. Diagnosing unexpected filename warnings: read the actual AudioMirror XML for the flagged track, compare `<Artists>` value vs the mirror filename directly. Fix is to rename the MP3 file to match the tag (not a code change).

## Testing Patterns

- **`_scanAheadBatchAlbumCounts` was null (not empty dict) in the test constructor.** This made it impossible to inject batch album count data for routing tests without real TagLib MP3 files. Fixed 2026-06-27: default test constructor now initializes to empty dict; injector overload `MusicIntegrator(string testLibraryPath, Dictionary<string,Dictionary<string,int>> batchAlbumCounts)` added for routing tests that need batch data without real MP3 files. When adding scan-ahead fields: always initialize to empty collections in test constructors, not null.


- **Path-injection pattern for testability.** Any class that hardcodes a `Constants.X` path (cache, mirror, config, last-run file) is made testable by adding an optional `string path = null` final parameter and computing `string effectivePath = path ?? Constants.X` at the top. Pass the real field at call sites; tests pass a temp path. Zero production behavior change, backwards compatible, no DI framework. Applied to: `Parser(mirrorPath, cachePath)`, `CountAkiraTheDonPersonSongs(person, libraryPath)`, `CountAkiraTheDonPersonAlbumSongs(person, album, libraryPath)`, `AgeChecker(force, lastRunInfoPath)`, `LibChecker.LoadExceptions(exceptionsPath)`. Use this before reaching for any heavier seam.

- **LibChecker test RelPath must have a leading backslash** (`\Artists\Artist\Singles\file.xml`). LibChecker's `GetRelPathPart(tag, 1)` splits on `\` and expects index 1 = the main folder, which only holds when the path leads with a separator (matching real AudioMirror format). Without it, index 1 resolves to the artist name and every folder-scoped rule silently no-ops. The `ArtistTag()` helper in LibCheckerTests documents this in a comment.

- **Pure-decision extraction for I/O-gated logic.** `AudioMirrorCommitter.GetSkipReason(bool, CommitTrigger)` was extracted from the void `TryCommit` so the commit-gate safety rules (skip-on-incremental, skip-on-dirty, proceed-when-clean) are unit-testable without a git repo. When a void method's risk lives in its decision rather than its side effects, extract the decision as a pure function and test that.

- **Stub `.mp3` files work for count-only paths.** `RoutingFixtures` creates `new byte[0]` placeholder .mp3 files. Safe only where the code calls `Directory.GetFiles(*.mp3).Length` without opening them (library-side counts). The ATD `Singles/` TagLib# scan is bypassed in tests because the fixture creates no `Singles/` folder - `Directory.Exists` returns false before any read.

- **`--verify` flag pattern for combining test modes.** When two test runners need to be called and their results combined, add a `--verify <manifestPath>` handler in Program.cs that calls each runner with out-params `(out int passed, out int failed)`, short-circuits on first failure, then prints a combined banner. The bat calls `--verify` and exits. The runners keep their original `Run()` signatures for backward compat via overloads. Applied 2026-06-02 to combine `--test` (TestRunner) + `--routing-manifest` (ManifestRunner). Use this pattern for any future multi-mode verification step.

## XML Generation and Declaration

**TrackXML.Write() emits explicit XML declaration:** `new XDeclaration("1.0", "utf-8", null)` as the first parameter to XDocument constructor. This produces `<?xml version="1.0" encoding="utf-8"?>` at the top of every generated track XML. Benefits: explicit encoding contract, clarity for external tools, standard practice. Cost: negligible (305 KB per 5653 files). Always include when generating XDocument - it's not overhead, it's contractual clarity.

## Mirror Generation Architecture: Stub-to-Replacement Pattern

**Reflector creates text-file stubs with MP3 paths; Parser reads MP3s via TagLib#; TrackXML overwrites stubs with actual XML.** This is the current design, not a bug - it works. However, the stubs are never read as input, only as intermediate placeholders. The pattern is vestigial: Reflector writes stub (path file) → Parser reads MP3 and caches → TrackXML overwrites stub with XML. The stubs exist but are never actually parsed - they're just temporary. Not a performance issue now, but worth refactoring if parsing becomes a bottleneck (Reflector could skip stub creation and TrackXML could read MP3 directly, bypassing the temp file stage entirely). Current design trades immediate clarity (the stubs are there) for simplicity (Reflector doesn't need to import TagLib# or know about XML generation).

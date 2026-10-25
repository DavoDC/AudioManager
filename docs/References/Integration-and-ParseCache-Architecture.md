# Integration and ParseCache Architecture

Split from `DevContext.md` (doc maintenance pass). Stage 3 integration architecture and ParseCache detail, moved verbatim.

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

# AudioManager Safety Review

**Date:** 2026-05-05  
**Review Scope:** Program against stated safety rules for library operations.

---

## Safety Rules Summary (as stated)

1. **TAGfixer can only run on new music! Never on library!**  
   Not comfortable running it on library because it could break tags for many files and be hard to fix.

2. **In library, program can only:**
   - Move files
   - Make folders
   - Delete duplicates (as user-approved decisions)
   - Nothing else

---

## Code Analysis

### Operation 1: TagFixer

**Rule Status: ✅ COMPLIANT**

TagFixer operates **exclusively on NewMusic folder** (`Constants.NewMusicPath`):

```csharp
var files = Directory.Exists(Constants.NewMusicPath)
    ? Directory.GetFiles(Constants.NewMusicPath, "*.mp3", SearchOption.AllDirectories)
    : Array.Empty<string>();
```

**Operations performed (NewMusic only):**
- Remove parentheticals from Title/Album tags
- Extract and fix featured artists (TPE1 tag)
- Set TCMP = 1 (compilation flag)
- Auto-fix genre for Musivation/Motivation tracks
- Auto-delete Akira The Don instrumentals
- Rename files to match naming convention (Artist - Title.mp3)

**Library impact: NONE.** TagFixer never touches the Audio library files.

---

### Operation 2: MusicIntegrator - Library Operations

**Rule Status: ⚠️ MOSTLY COMPLIANT - One dangerous edge case**

MusicIntegrator has three phases:

#### Phase 1: Tag Fixing (NewMusic only)
Calls TagFixer constructor - operates on NewMusic only. ✅

#### Phase 2: Duplicate Detection & Resolution
Scans NewMusic + AudioMirror XML to detect duplicates.

**User decision handling:**
- **D (keep library):** Deletes duplicate from **NewMusic** (not library). ✅
- **L (replace library):** **Deletes file from Audio library, then routes new file.** (see below)
- **K (keep both):** Routes new file normally. ✅

**Code snippet (L decision):**
```csharp
if (File.Exists(dup.LibraryFilePath))
{
    File.Delete(dup.LibraryFilePath);  // <-- DELETES FROM LIBRARY
    PrintTimestamped($"  Deleted from library: {dup.RelLibraryPath}");
    // Then: SkipRouting stays false, new file is routed
}
```

This is **rule-compliant** (deleting duplicates), BUT depends entirely on accurate duplicate detection.

#### Phase 3: File Routing
Operations on NewMusic:
- Move files from NewMusic → Audio library destinations
- Create destination folders (if needed)

```csharp
Directory.CreateDirectory(destDir);      // ✅ Make folders
File.Move(sf.SourcePath, destPath);      // ✅ Move files
```

**Library impact:** Files moved INTO library, destination folders created. ✅ Compliant.

---

## Vulnerability Analysis

### Issue #1: Duplicate Detection Accuracy

**Risk Level: 🟡 MEDIUM (mitigated by metadata-based matching)**

The safety of the L (replace library) decision depends on **duplicate detection being correct.**

**Detection logic** (FindDuplicateInMirror):
- Compares **primary artist + title** of incoming file against all AudioMirror XMLs
- Case-insensitive, whitespace-trimmed matching
- Returns matching XML file path if found
- No hash-based comparison

**Why this is safer than hashes:**
- Metadata-based (artist/title) is more reliable than file content
- Catches true duplicates even if file has different bitrate/encoding
- Harder to false-positive (requires both artist AND title to match)

**Edge case risk:**
- If a file has its primary artist or title corrupted/missing: won't detect duplicate
- If artist or title differs even slightly: won't detect duplicate (requires exact match after whitespace trim)
- If the AudioMirror is stale or has errors in artist/title fields: detection fails

**Current safeguard:** User review + dry-run mode before real execution + visual confirmation.

**Recommendation:** 
1. Before running any integration with L decisions, dry-run first to see what would be deleted
2. Verify that AudioMirror XMLs have clean artist/title fields (run Analysis mode first)
3. Spot-check any L decisions by manually looking at the affected library file

---

### Issue #2: Tag Modifications in NewMusic Not Validated

**Risk Level: 🟡 MEDIUM**

TagFixer modifies tags in NewMusic with no library rollback plan:

1. Files in NewMusic have tags fixed/changed
2. Files are then routed into library
3. If routing fails halfway through (e.g., corruption during move, OS error), tags in NewMusic are already modified and may not be recoverable

**Example scenario:**
- TagFixer removes parenthetical from "(feat. Artist)" 
- Move to library fails due to filesystem error
- File is partially written, tags already changed
- Original "(feat. Artist)" version is lost

**Current safeguard:** None. TagFixer operates in-place on NewMusic files.

**Recommendation:** Consider backing up NewMusic before running TagFixer, or implement tag rollback on routing failure.

---

## Operation Compliance Matrix

| Operation | Scope | Action | Compliant? | Safety Concern |
|-----------|-------|--------|-----------|-----------------|
| TagFixer cleanup | NewMusic | Modify tags, rename files | ✅ Yes | None (NewMusic only) |
| Duplicate detection | AudioMirror XML | Read + hash compare | ✅ Yes | Accuracy depends on hash logic |
| D decision (keep lib) | NewMusic | Delete duplicate | ✅ Yes | None |
| L decision (replace) | Library | Delete old, route new | ✅ Yes | Only if duplicate detection correct |
| K decision (keep both) | NewMusic → Library | Move file | ✅ Yes | None |
| File routing | NewMusic → Library | Create folders, move | ✅ Yes | None |
| LibChecker | Library | Read-only validation | ✅ Yes | Read-only, no modifications |
| Reflector | AudioMirror | Regenerate XML | ✅ Yes | Only modifies mirror, not library |

---

## Required Constraints (from CLAUDE.md)

**From CLAUDE.md:**
> Only the user (David) executes integration. Claude implements features and prepares workflows, but stops before running any integration.

**Status: ✅ ENFORCED IN CODE**

MusicIntegrator is only invoked from Program.Main when user selects menu option or via CLI args. No automatic execution. User must manually trigger via `launch.bat`.

---

## Recommendations

### Overall Status
The program **is compliant with the stated safety rules.**

### Safety Best Practices

**Before any real integration:**
1. Always run **Analysis mode** first to regenerate fresh AudioMirror
   - Ensures metadata is current and clean
   - Catches any corrupted artist/title fields before integration
2. Always run **Integration (Dry Run)** before real execution
   - Prints all proposed moves and deletions
   - Lets you verify that duplicate detection is correct
   - Zero risk - no files modified
3. Only then run **Integration (Real)** after reviewing dry-run output

**If considering L decisions (replace library):**
4. During dry-run review, visually verify each L decision
   - Check that the library file is truly a duplicate
   - Not just a false positive from close artist/title match
5. Monitor for any files deleted that shouldn't have been
6. If a file is accidentally deleted, restore from backup or re-download

### The "What If" Issue

Your comment references "what if issue" - if you're concerned about a specific failure mode or edge case in duplicate detection, add it to AudioManager IDEAS.md and I can implement a safeguard or improve the detection logic.

---

## Code Locations

- **TagFixer:** `project/AudioManager/Code/Doer/TagFixer.cs`
- **MusicIntegrator:** `project/AudioManager/Code/Doer/MusicIntegrator.cs` (duplicate logic, routing, L decision)
- **Program entry:** `project/AudioManager/Code/Program.cs` (user-triggered only)


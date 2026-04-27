# CRITICAL: TagFixer Missing - Blocking Stage 3C

**Date:** 2026-04-27  
**Status:** 🛑 BLOCKING REAL INTEGRATION

---

## THE ISSUE

You're about to run Stage 3C (real integration) with 126 tracks whose **tags have NOT been touched**.

The AudioManager integration system has a **critical missing component: TagFixer**.

**Current design (incomplete):**
1. User drops 126 raw MP3s in NewMusic folder (tags dirty - filenames wrong, featured artists missing, unwanted words in titles)
2. Integration runs (assumes tags are clean) → moves files to library folders
3. Post-integration LibChecker validation runs → **FLAGS ISSUES** because tags weren't cleaned
4. Integration "passes" but library has problems

**Expected design (what you want):**
1. User drops 126 raw MP3s in NewMusic folder
2. **TagFixer runs** (cleans ALL tags automatically) → produces clean files
3. Integration runs (purely routes already-clean files) → moves to correct folders
4. Post-integration LibChecker → **PASSES CLEAN** because all tags were pre-fixed
5. Commit and done

---

## WHAT'S MISSING: TagFixer Module

**Purpose:** Automatically clean tags on all NewMusic files BEFORE integration runs.

**Responsibilities (per Music-Library-Rules.md):**

1. **Remove unwanted words from tags:**
   - Remove from Title and Album: "(feat. ...)", "(ft. ...)", "(Album Version)", "(Explicit)", "(Edit)", "(Radio Edit)", "(Original)", "(Remix)", "(Version)"
   - But preserve legitimate "feat." info in TPE1 (artist tag)

2. **Fix featured artists in TPE1 (artist tag):**
   - Current filename: `Chiddy Bang - Mind Your Manners (feat. Icona Pop).mp3`
   - After TagFixer: TPE1 = `Chiddy Bang;Icona Pop` (semicolon-separated, primary first)

3. **Rename files to standard convention:**
   - Format: `{all-artists-semicolon-separated} - {title}.mp3`
   - Example: `Chiddy Bang;Icona Pop - Mind Your Manners.mp3`

4. **Set TCMP = 1:**
   - Compilation flag on every track (prevents iTunes from treating each song as separate album)

5. **Set genre for Musivation/Motivation tracks:**
   - If artist is Akira The Don or Loot Bryon Smith → set Genre = "Musivation"
   - Any other Motivation-tagged tracks → set Genre = "Motivation"

6. **Report what was fixed:**
   - Per-file log: filename, what was changed, any errors
   - Summary: total files processed, fixes applied, errors encountered

---

## CURRENT SITUATION

### What Integration Does (PreProcessTags method in MusicIntegrator.cs)
- ✅ Sets TCMP=1
- ✅ Sets genre for Akira The Don tracks
- ❌ Does NOT remove unwanted words from tags
- ❌ Does NOT ensure featured artists are in TPE1
- ❌ Does NOT rename files

**Result:** Integration will move files with dirty tags. LibChecker will flag issues post-integration.

### What TagFixer Should Do
- ✅ Everything above
- ✅ All tag cleanup rules from Music-Library-Rules.md
- ✅ All filename fixes
- ✅ Detailed report of what was fixed

**Result:** Clean files passed to Integration. Integration only routes. LibChecker validation passes clean.

---

## ACTION REQUIRED BEFORE STAGE 3C

### Option A: Implement TagFixer (recommended)
If you want to run real integration with automated tag cleanup:

1. Create **TagFixer** module (C# class or separate tool)
2. Implement tag cleaning logic above
3. Call it BEFORE integration: `AudioManager tagfix` or `tagfix` submenu in launcher
4. Then run `integrate`

See **IDEAS.md TIER 0 BLOCKER** for implementation guidance.

### Option B: Manual Tag Cleanup (workaround for this batch only)
If you want to proceed NOW without implementing TagFixer:

1. Open all 126 NewMusic files in MP3Tag
2. Apply rules from Music-Library-Rules.md:
   - Remove "(feat. ...)", "(Album Version)", "(Explicit)" from Title and Album
   - Ensure featured artists are in Artist field, primary first, semicolon-separated
   - Rename files using tag-to-filename: `%artist% - %title%`
   - Set TCMP = 1 on all tracks
   - Set genre for Akira/Loot Bryon Smith tracks
3. Save all files
4. Then run Integration

**Time estimate:** 30-60 minutes for 126 tracks manually

---

## CRITICAL DESIGN PRINCIPLE

**Integration should ONLY route files. TagFixer should handle ALL tag work.**

Why?
- **Separation of concerns** - each tool does one thing well
- **Testability** - can test routing independently of tag fixing
- **Safety** - integration failures are routing issues, not accidental tag corruption
- **Automation** - tag fixing is deterministic and automatable; routing has some user choice
- **Reusability** - TagFixer can be used standalone or as part of the analysis pipeline

---

## DOCUMENTATION UPDATES

Updated to clarify this design principle:
- **IDEAS.md:** TIER 0 now has TagFixer as BLOCKING item
- **Music-Library-Rules.md:** Marked all tag rules as "AUTOMATED VIA TAGFIXER"
- **Music-Discovery-Workflow.md:** Stage 3 now shows two-step process: TagFixer → Integration
- **Stage-3C-Pre-Integration-Review.md:** Flags that PreProcessTags is incomplete

---

## NEXT STEPS

1. **Decide:** Implement TagFixer now, or manually clean tags for this batch?
2. **If TagFixer:** Add to IDEAS.md TIER 0, implement in next session
3. **If manual cleanup:** Do tag cleanup in MP3Tag, then proceed with real integration
4. **Post-integration:** Verify LibChecker validation reports CLEAN (zero issues)

Either way: this must be resolved before Stage 3C succeeds with library validation passing.


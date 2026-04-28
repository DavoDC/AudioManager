# STAGE 3C: Real Integration - Execute

**Date:** 2026-04-28  
**Status:** BLOCKED - Prerequisites not met (awaiting decision logging implementation)  
**Approved tracks:** 51 tracks staged in `C:\Users\David\Downloads\NewMusic\PROCESSED\`

---

## Current Blockers

Before Stage 3C can proceed, one TIER 1 prerequisite must be implemented:

### ⚠️ CRITICAL: Decision Logging Not Implemented

**Requirement:** Integrator must log all routing decisions to XML BEFORE files are moved. Once integration completes, this metadata is lost forever.

**What must happen:**
1. Implement `DecisionLog` class in AudioManager to capture routing decisions
2. Integrate logging into `MusicIntegrator.GetDestDir()` and `FolderPickerHandler`
3. Each decision must record: artist, album, track, sourceFile, destinationPath, routingReason, timestamp
4. Output to `docs/Historical/WorkflowExecution-YYYY-MM-DD/decisions.xml`
5. Test with `--dry-run` mode to ensure logging works without moving files

**Why this matters:** This integration batch is the first real test of the routing pipeline. The decisions made will reveal patterns (which routing rules fire for which tracks, edge cases). These patterns improve the integrator's logic for future batches. Without logging, that knowledge is lost.

**Next step:** `/dev-session` to implement TIER 1 decision logging, then Stage 3C can proceed.

See `IDEAS.md` TIER 1 section for full specification.

---

## Prerequisites (After Decision Logging Implemented)

Before executing Stage 3C:

### Pre-Flight Checks

- [ ] **Decision logging feature implemented and tested** (TIER 1 complete)
- [ ] **AudioMirror fresh** - library snapshot captured (integration gate checks this)
- [ ] **LibChecker clean** - no violations in current library (integration gate checks this)
- [ ] **51 tracks staged** - all approved music in `C:\Users\David\Downloads\NewMusic\PROCESSED\`

---

## Stage 3C Workflow (Once Unblocked)

### Step 1: Dry-Run Preview

**Launch:** `scripts/launch.bat` → Select **"3. Integration (dry-run)"**

This shows:
- Where each track will be routed (Artists/Musivation/Motivation/Misc)
- Reasoning for each decision (artist auto-route, genre rule, scan-ahead threshold, manual folder-picker)
- Folder structure that will be created
- Which existing Misc songs require manual migration (if any)

**Action:** Review the preview. If routing looks wrong, this is the time to spot issues.

### Step 2: Manual Decisions (During Dry-Run Review)

For any tracks routed to **Misc**, review:
- Is this a one-off single or a new artist?
- Should it go to Artists folder if future batches have more tracks?

For any tracks with **manual folder-picker prompt**, you'll be asked during the real run:
- Is this a Sources/Films, Sources/Shows, Sources/Anime track?
- Override routing destination if needed

### Step 3: Execute Integration

**Launch:** `scripts/launch.bat` → Select **"4. Integration (real)"**

This executes the actual file movements:
- Moves 51 tracks from `C:\Users\David\Downloads\NewMusic\PROCESSED\` to `C:\Users\David\Audio\` subfolders
- Logs all decisions to `docs/Historical/WorkflowExecution-2026-04-28/decisions.xml`
- Regenerates AudioMirror XMLs to reflect new files
- Runs LibChecker validation post-integration
- Reports result: CLEAN (expected) or ISSUES FOUND (fix first)

**Expected outcome:** All 51 tracks moved, AudioMirror updated, decision log populated, library passes LibChecker.

### Step 4: Post-Integration Actions

- [ ] **Verify LibChecker result** - should be CLEAN
  - If CLEAN: proceed to Step 5
  - If issues: fix library and re-run `scripts/launch.bat` → **"1. Analysis"**
- [ ] **Commit AudioMirror changes** - manually in GitHub Desktop (auto-commit is disabled)
  - Stage `AudioMirror/AUDIO_MIRROR/` folder
  - Commit with message like "2026-04-28 Batch integration (51 tracks)"
  - Push to origin
- [ ] **Review decision log** - examine `docs/Historical/WorkflowExecution-2026-04-28/decisions.xml`
  - Extract routing patterns: which rules fired, for which tracks
  - Document findings (e.g. "Lupe Fiasco always routed to Artists", "Akira Don tracks always Musivation")
  - Update integrator heuristics if patterns suggest improvements

---

## Known Behavior

- **Auto-commit disabled:** AudioMirror changes are not automatically committed (for safety). You manually commit in GitHub Desktop.
- **Misc folder routing:** If files route to Miscellaneous Songs, the integrator may prompt you for confirmation before moving.
- **Duplicate detection:** If any track matches an existing artist+title in the library, you're asked to delete it from NewMusic or keep both.
- **Folder creation:** Artist folders and album subfolders are created automatically as needed.

---

## What Happens Next

After Stage 3C completes successfully:

1. **TIER 1 validation** - Confirm routing decisions are captured and patterns extracted
2. **TIER 2 work** - Robustness: add automated tests for routing logic, investigate performance
3. **Future batches** - Same workflow: Stage 3B (review) → Stage 3C (integrate with decision logging) → TIER 1 analysis

---

## References

- `IDEAS.md` - TIER 0 (safety) and TIER 1 (decision logging prerequisite)
- `docs/Development/Music-Library-Rules.md` - routing rules and library structure
- `STAGE_3B_REVIEW_MUSIC_(COMPLETE).md` - inventory of 51 approved tracks
- `AudioMirror/` repo - library snapshot and validation state

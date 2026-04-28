# STAGE 3C: Real Integration - Execute

**Date:** 2026-04-28  
**Status:** ✓ READY FOR DRY-RUN - All prerequisites complete  
**Approved tracks:** 51 tracks staged in `C:\Users\David\Downloads\NewMusic\PROCESSED\`

---

## Prerequisites Complete ✓

### Decision Logging - IMPLEMENTED (2026-04-28)

✓ `DecisionLog` class captures all routing decisions with full track metadata  
✓ Integrated into `MusicIntegrator` - logs at routing points (auto-route, manual selection, dry-run)  
✓ Output to `docs/Historical/WorkflowExecution-YYYY-MM-DD/decisions.xml` with dryRun flag  
✓ Tested: build succeeds, ready for dry-run validation  

**What it logs:**
- Full track metadata (artist, primaryArtist, title, album, year, genres, compilation)
- Routing decision (destination path, routing reason)
- Execution context (dryRun: true/false, timestamp)
- Enables pattern extraction and audit trail for this batch

---

## Next Steps - Immediate Actions

**Status:** Ready for dry-run testing. All prerequisites met.

### Immediate Action: Test Dry-Run (2026-04-28)

Run the dry-run integration to validate routing decisions before moving files:

```powershell
.\scripts\launch.bat
→ Select Option 3 (Integration - Dry Run)
```

**This will show:**
- Tag fixes preview (TCMP, genres, parentheticals, file renames)
- Routing decisions for all 51 tracks (where each will go, why)
- New folders that will be created
- Which Misc songs need manual migration (if any)

**Expected output:**
- `logs/integration-2026-04-28-HHMMSS-dryrun.md` - integration log (timestamped for multiple runs)
- `logs/decisions-2026-04-28-HHMMSS.xml` - routing decisions (marked dryRun: true, timestamped)

**After dry-run:** Review both files, verify routing looks correct. Then proceed to real integration.

---

## Stage 3C Workflow - Ready Now

### Step 1: Dry-Run (Validation Phase)

**Command:** `.\scripts\launch.bat` → Option 3 (Integration - Dry Run)

**What happens (no files moved, just preview):**
1. **Tag Fixing Preview** - Shows what would be fixed in each file
   - TCMP set to True
   - Genres corrected (Musivation for Akira The Don, etc.)
   - Parentheticals removed
   - Featured artists moved to TPE1 tag
   - File renames per convention

2. **Routing Preview** - Shows where each track will go
   - Decision for each of 51 tracks (Artists/Musivation/Motivation/Misc)
   - Reasoning (artist auto-route, genre rule, scan-ahead, manual)
   - Existing Misc songs flagged if they need manual migration
   - Folder structure that will be created

3. **Decision Log** - Routes logged to `decisions.xml` (marked dryRun: true)
   - Audit trail of all routing decisions
   - Full track metadata captured
   - Can extract patterns after this batch

**Action:** Review both `integration-2026-04-28-dryrun.md` and `decisions.xml`. If routing looks wrong, fix library and re-test. If OK, proceed to Step 2.

### Step 2: Review Dry-Run Decisions

After dry-run completes, review the outputs:

**File 1:** `logs/integration-2026-04-28-dryrun.md`
- Check per-file routing decisions
- Verify confidence report (new folders, destination sanity check, errors)
- Any files routed to Misc that should go elsewhere?

**File 2:** `logs/decisions-2026-04-28-HHMMSS.xml`
- Sample the routing decisions (artist → destination → reason)
- Verify track metadata is being logged correctly
- Check timestamp and dryRun: true flag

**Possible actions:**
- If routing looks correct → go to Step 3 (real integration)
- If routing looks wrong → pause, fix the library or routing rules, re-test with another dry-run
- If tag fixes look wrong → fix TagFixer rules, rebuild, re-test

### Step 3: Execute Real Integration

**Command:** `.\scripts\launch.bat` → Option 4 (Integration - Real)

**What happens (files actually moved):**
1. **Tag Fixing** - Applies all tag fixes to NewMusic files (TCMP, genres, etc.)
2. **Routing** - Moves files to destinations (Artists/Musivation/Motivation/Misc)
3. **AudioMirror Update** - Regenerates XML snapshot to reflect new files
4. **LibChecker Validation** - Runs library checks (expected: CLEAN)
5. **Decision Logging** - Routes logged to `decisions.xml` (marked dryRun: false)

**Expected outcome:** All 51 tracks moved, AudioMirror updated, library CLEAN, decisions logged.

**Confirmation prompt:** You'll be asked "Type YES to confirm" before files are moved. This is your last chance to abort.

### Step 4: Post-Integration Validation & Cleanup

After integration completes successfully:

- [ ] **Verify LibChecker result** - Check console output
  - Expected: "Post-integration validation: CLEAN"
  - If ISSUES FOUND: Fix library and re-run `.\scripts\launch.bat` → Option 1 (Analysis)

- [ ] **Commit AudioMirror changes**
  - Open GitHub Desktop
  - Stage `AudioMirror/AUDIO_MIRROR/` folder (files that changed)
  - Commit with message: "2026-04-28 Batch integration (51 tracks)"
  - Push to origin (auto-commit is disabled for safety)

- [ ] **Extract routing patterns** (TIER 1 analysis)
  - Review `logs/decisions-2026-04-28-HHMMSS.xml` (most recent run)
  - Identify patterns: "Akira The Don → 100% Musivation", "New artist with 3+ tracks → new Artists folder", etc.
  - Document at least 3 patterns in HISTORY.md for future optimization

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

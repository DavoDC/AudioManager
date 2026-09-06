# Execution-Record Fallback Removal: Design Decision

**Status: design settled, ready for mechanical Sonnet implementation.** This is IDEAS.md action item 3 of the three that came out of David's 2026-09-06 review of the execution-record contract. Prerequisites, read first and not repeated here: `docs/References/Execution-Record-Contract-Research.md` and `docs/References/Execution-Record-Contract-Design.md` (the shipped contract). Item 1 (partial-trust remap, commit `a7d5ba8f`) is the baseline this builds on. Item 2 (old unreadable `logs/routing-*.json` files) is out of scope and must not be touched here.

The implementer follows this document as a checklist and makes no judgment calls. If any instruction here appears to require touching something section 1 marks as forbidden, **stop and escalate rather than improvising**.

---

## 1. Escalation-boundary ruling (read before anything else)

**Ruling: NO - this change does not fall inside the guarded path, and the guarded set is NOT being grown.** Identical reasoning to the prior design doc's section 1: CLAUDE.md's boundary is the path from a decline click to the exe's argument list. Everything specified below happens *after* `runner.run` has returned, and changes only how the GUI reports and gates on what already happened. It cannot cause a declined file to be moved.

**FORBIDDEN, no exceptions - the implementer must not modify any of:**

- `_write_manifest` (`gui/tabs/integration.py`), including its manifest content, its timestamped path, and its returned `args` list
- `IntegrationState.accepted`
- `IntegrationState.declined`
- the `args` list passed to `runner.run` in `run_execute`

Confirmed: **this design requires touching none of them.** Every edit in section 4 is at or below the `_finish_execute` / display layer, plus one additive `IntegrationState` field pair, plus one non-behavioural C# change inside `WriteExecutionRecord`'s own `catch`.

Sensitive (needs David's eyes before shipping, as a condition of this changeset, not a permanent boundary expansion):

| Sensitive | Freely editable |
| --- | --- |
| `_finish_execute` - it now decides whether a batch is reported as verified at all | `stage_execute`, `EXEC_STATUS_LABELS`, `gui/theme.py` CSS, all docstrings |
| The new `unknown` terminal status and the acknowledgement gate | `IntegrationState` field additions, `run_execute_simulated`, all tests |
| `MusicIntegrator.WriteExecutionRecord`'s catch block | Everything else in this document |

---

## 2. The ruling on David's instruction (read before section 3)

David's words: *"my instinct is stop fully coz something gone wrong and old way is unreliable. Maybe we should fully remove old bad way? No fallback?"*

**I agree with the substance and I am implementing it, with one deliberate narrowing that David should confirm.** Stated plainly so he can overrule it:

**Agreed and implemented in full:** the console-derived account never survives into a finished batch's reported outcome. Today, when the record is missing, `_finish_execute` sweeps `queued`/`moving` to `done`, counts those swept values into "Integration complete - N moved", and appends a soft note. That whole path is the "old bad way" and it is **deleted outright**, not softened. `_failed_filename_from_output` exists only to serve that path and is **deleted too**, along with its tests. No success sentence, no moved/skipped/failed counts, and no per-track outcome is ever again derived from console text.

**Narrowed, deliberately:** `_update_exec_status` itself is **kept**, restricted to what it already claims in its docstring - live in-flight progress while the exe is still running. Deleting it would remove the progress bar and per-track feedback during a real integration and buy zero data safety, because nothing it writes survives to the finished state under this design: on the missing-record path every status it wrote is **overwritten with `unknown`** before anything is displayed as final. The unreliable thing is not the function, it is trusting its output after the run ends - and that trust is what gets removed. If David wants the function gone as well, that is a one-line follow-up (drop the `on_line` call and the progress bar goes static), but I recommend against it.

**Why "stop fully" cannot mean "prevent the operation":** by the time the record fails, the exe has exited and files are already moved. So "stop" is redefined here as: the GUI **refuses to characterise the batch**, shows a hard failure banner, marks every track `unknown`, and **blocks the path back to a new batch behind an explicit acknowledgement**. That is the strongest honest meaning available post-hoc.

**Tradeoff I am accepting, named explicitly:** a benign JSON write hiccup - a locked `logs/` directory, an antivirus scanner holding the file, a disk-full moment - on an otherwise perfectly successful batch will now present as an alarming hard-stop with no per-track detail and no counts, where today it degrades gracefully to a mostly-correct account with a caveat. David's stance wins because the two failure modes are not symmetric: the cost of the strict path is a scary screen after a fine run, recoverable by reading `logs/` or the run log; the cost of the lenient path is a *confident and wrong* account of irreversible file moves, which is the exact harm this whole line of work exists to eliminate, and which the user has no signal to distrust. An unhelpful-but-honest report is strictly better than a helpful-but-possibly-false one when the subject is "where did my music files go". Section 4.6 mitigates the recoverability cost directly by naming the log paths in the banner.

**One consequence David should be aware of, because it makes the strict path fire routinely rather than rarely:** a user-cancelled real run also produces no execution record (the exe is killed before `WriteExecutionRecord` runs). Under this design, cancelling now lands in `unknown`-everything plus the gate, every time, rather than the current tidy "everything not run". This is honest - a cancelled run genuinely may have moved some files with no record of which - but it turns a routine action into a hard-stop. Section 4.4 therefore gives cancellation its own calmer wording while keeping identical status semantics and the identical gate. Do not give it a softer *behaviour*.

---

## 3. Concrete behaviour specification

### 3.1 New terminal status: `unknown`

A seventh `exec_status` value, `unknown`, meaning: **this file may or may not have been moved; the GUI cannot tell.** It is distinct from every existing value and must never be conflated with them:

- not `done` - would assert a move that may not have happened
- not `notrun` - would assert the file is still in NewMusic, which is the more dangerous lie of the two
- not `failed` - would assert an error the GUI has no evidence of
- not `unverified` - that value already means "the record named this row with a status I don't recognise", i.e. a record exists and covers the row. Keep the two separate; they have different causes and different remedies.

Label text: `"unknown"`. CSS class `.st-unknown`, styled with the danger colour `var(--accent4)` (not `--accent3`, which `notrun`/`unverified` share) so a batch with unknown rows reads as worse than one with skipped rows.

### 3.2 Three record outcomes, three behaviours

| Outcome | Condition in `run_execute` | Result |
| --- | --- | --- |
| **A. Record present** | `execution_record_path` resolved and `parse_execution_record` succeeded | Exactly today's behaviour, unchanged, including the item-1 partial-trust remap. Nothing in this design alters the happy path. |
| **B. Record absent** | `execution_record_path` returned `None` | Verification-failed state, `reason = "missing"` |
| **C. Record unreadable** | a path resolved but parsing raised | Verification-failed state, `reason = "unreadable"`, and the offending path is retained and shown |

B and C share all behaviour and differ only in banner wording and whether a path is displayed. They are separated because the remedies differ: B means the exe never wrote one (crash, cancel, or a `WriteExecutionRecord` failure); C means a file exists on disk and can be inspected by hand.

### 3.3 What the verification-failed state does

1. **Every** entry in `S.exec_status` is set to `unknown` - unconditionally, for all `S.exec_targets`, regardless of what `_update_exec_status` wrote during the run and regardless of `result.ok`.
2. `S.confidence_report` is `None` (already the case), so `confidence_report_strip()` renders nothing. Do not synthesise one.
3. `S.exec_summary` contains **no counts and no outcome claim**. See 4.4 for the exact strings.
4. A red banner is rendered above the summary (section 4.6).
5. The "New scan" button is disabled until acknowledged (section 4.7).
6. `S.exec_ok` still records `result.ok` as it does today - it describes the process exit, not the file outcomes, and the error/cancelled modals continue to fire exactly as they do now.

---

## 4. File-by-file changes

### 4.1 `gui/tabs/integration.py` - `IntegrationState`

Replace the single `self.exec_record_missing = False` field with three, keeping the existing one (it is referenced by tests and by `stage_execute`; keep the name and its meaning):

```
self.exec_record_missing = False      # True: no execution record could be found or parsed - outcomes are UNKNOWN
self.exec_record_fail_reason = ""     # "" | "missing" | "unreadable" | "cancelled"
self.exec_record_path = ""            # str path of the record that failed to parse ("unreadable" only)
self.exec_unknown_ack = False         # user has acknowledged the unverified batch; ungates "New scan"
```

All four reset in `run_execute` and `run_execute_simulated` at the same place `S.exec_summary = ""` is reset today (`exec_record_missing` to `False`, the strings to `""`, `exec_unknown_ack` to `False`).

### 4.2 `gui/tabs/integration.py` - `run_execute`

Only the block after `runner.run` returns changes. Replace the current resolve-and-parse block with one that records *why* it failed:

- `path = routing.execution_record_path(result.lines, started)`
- If `path` is `None`: `record = None`, `S.exec_record_fail_reason = "missing"`, `S.exec_record_path = ""`.
- Else attempt `routing.parse_execution_record(path)` inside the same `try` catching the same exception tuple `(OSError, ValueError, json.JSONDecodeError, SchemaVersionError)`. On exception: `record = None`, `S.exec_record_fail_reason = "unreadable"`, `S.exec_record_path = str(path)`.
- If `result.cancelled` and `record is None`, overwrite `S.exec_record_fail_reason = "cancelled"` (cancellation is the known cause and outranks "missing" for wording purposes).
- Then `_finish_execute(result, record)` as today.

Setting these on `S` from `run_execute` rather than passing them as arguments keeps `_finish_execute`'s signature stable for `run_execute_simulated` and for the existing tests that call it directly. `_finish_execute` must default `S.exec_record_fail_reason` to `"missing"` when it is empty and `record is None`, so a direct `_finish_execute(result, None)` call still produces a coherent state.

**Nothing above the `runner.run` call changes.** `targets`, `_write_manifest`, `args` untouched (section 1).

### 4.3 `gui/tabs/integration.py` - `_finish_execute`, the `record is not None` path

Unchanged. Do not touch the `_RECORD_STATUS_MAP` loop, the `unverified_files` partial-trust logic, the success-summary sentence, or the `n_notrun` counting in the record-present failure branch. Item 1's behaviour survives intact.

### 4.4 `gui/tabs/integration.py` - `_finish_execute`, the `record is None` path (the rewrite)

**Delete** all of the following from the function:

- the `if record is None:` sweep of `queued`/`moving` -> `done` inside the `result.ok` branch
- the entire `if record is None:` branch inside the `else` (not-ok) branch - the `_failed_filename_from_output` call, the `failed_name` comparison, the `n_notrun` accumulation
- the trailing `if S.exec_record_missing:` sentence append

**Replace** with a single early branch, placed immediately after `S.exec_record_missing = record is None` and before the `show_modal` logic:

- When `record is None`:
  - set every key of `S.exec_status` (iterate `S.exec_targets`, and also any key already present) to `"unknown"`
  - set `S.exec_summary` to exactly one of these three strings, chosen by `S.exec_record_fail_reason`:
    - `"cancelled"` -> `"Run cancelled - no execution record was written, so what happened to each file is UNKNOWN. Some files may already have been moved. Verify the library before running another batch."`
    - `"unreadable"` -> `"VERIFICATION FAILED - an execution record was written but could not be read, so what happened to each file is UNKNOWN. Some or all files may already have been moved. Verify the library before running another batch."`
    - anything else (`"missing"` or empty) -> `"VERIFICATION FAILED - the integration produced no execution record, so what happened to each file is UNKNOWN. Some or all files may already have been moved. Verify the library before running another batch."`
  - do **not** append the declined-files note, the statistics note, or any count
  - `n_notrun = 0` (so the not-ok branch's `if n_notrun:` append is skipped)
- The `result.ok` branch's success sentence is now built **only** when `record is not None`. Guard it accordingly; never let a count computed from `S.exec_status` reach the user when the record is absent.
- `result.interpreted("Integration")` on the not-ok path: **prepend** it to the verification-failed sentence rather than replacing it (the exe's own error text is still real evidence, it just is not a per-file account). Order: interpreted text, then a space, then the verification sentence.
- The cancelled/error modal decisions (`show_cancelled`, `show_modal`) are unchanged and still keyed off `result.ok` / `result.cancelled`.

Update the function docstring to state that a missing or unreadable record is now a terminal verification failure with no console-derived fallback, and cite this document.

### 4.5 `gui/tabs/integration.py` - delete `_failed_filename_from_output`

It has exactly one caller, which 4.4 deletes. Remove the function. Grep the whole repo for `_failed_filename_from_output` and confirm zero remaining references (including `gui/tests/`) before finishing. This is the concrete, literal piece of "fully remove old bad way" that can be honoured without losing live progress.

### 4.6 `gui/tabs/integration.py` - `_update_exec_status`, labels, and `stage_execute`

- `_update_exec_status`: **logic unchanged, do not delete.** Extend the docstring to say that when no record is available its output is discarded entirely and every row becomes `unknown` - it is live progress and nothing else, in both the record-present and record-absent cases.
- `EXEC_STATUS_LABELS`: add `"unknown": "unknown"`.
- `stage_execute`'s `done` count (the progress-bar denominator): add `"unknown"` to the tuple alongside `done`/`skipped`/`failed`/`unverified`, so a finished unverified batch still shows a full bar rather than a stalled one. The bar already forces 100% when `S.exec_done`, so this is belt-and-braces.
- `stage_execute`'s existing `if S.exec_record_missing:` strip is **replaced**, not extended. Render a `libchecker-strip dirty` block containing, in this order:
  1. the warning glyph and a heading matching the reason - `VERIFICATION FAILED - outcomes are UNKNOWN` for missing/unreadable, `Run cancelled - outcomes are UNKNOWN` for cancelled
  2. the sentence `Every track above is shown as "unknown" because the GUI has no trustworthy record of what the integrator did. Console output is not used as a substitute.`
  3. for `"unreadable"` only: `Record file that could not be read: <S.exec_record_path>` (HTML-escaped via `_esc`)
  4. the recovery line: `Check logs/ for an execution-*.json file and gui/.cache/run-logs/ for this run's raw output before starting another batch.` Use the real directory names from `gui/config.py` (`config.LOGS_DIR`, `config.RUN_LOGS_DIR`) rather than hardcoded strings if they are already imported; do not add new config entries.
- `gui/theme.py`: add `.st-unknown{color:var(--accent4);}` immediately after the existing `.st-unverified` rule.

### 4.7 `gui/tabs/integration.py` - the acknowledgement gate

Inside `stage_execute`, in the `if S.exec_done:` block, after the banner:

- When `S.exec_record_missing and not S.exec_unknown_ack`: render a `ui.checkbox` labelled `"I understand this batch could not be verified and I will check the library myself"` whose `on_change` sets `S.exec_unknown_ack = True` and calls `S.refresh()`. Render the "New scan" button **disabled** (`.props("disable")`) alongside it.
- When `S.exec_record_missing and S.exec_unknown_ack`: render the "New scan" button enabled as normal. Keep the banner visible - acknowledging does not hide the failure, it only unblocks the button.
- When `not S.exec_record_missing`: exactly today's unconditional enabled button.

The gate is deliberately session-local and one click deep. A harder lock (persisted, or requiring a typed confirmation) is not justified: the GUI has no durable store for it, and a gate the user cannot clear would strand them with no way back to the scan stage - which is itself a data-safety problem, because the remedy for an unverified batch is usually to re-scan and see what is still sitting in NewMusic.

The `_run_analysis_now()` path (wired to the cancelled modal's "Run Analysis Now" button) **must respect the same gate**: add an early return when `S.exec_record_missing and not S.exec_unknown_ack`, with a `ui.notify("Acknowledge the unverified batch first", type="warning")`. Otherwise cancellation - the most common route into this state - has a one-click bypass around the gate it just triggered.

### 4.8 `gui/tabs/integration.py` - `run_execute_simulated`

Unchanged in behaviour: it always constructs and passes a synthetic record, so it never enters the verification-failed path. Add the three new state resets from 4.1 to its reset block. Do not add a simulated failure mode here - `IntegrationState.accepted` already guarantees Simulate can never fail mid-batch, and inventing a fake unverified run would be the one place in the GUI where an alarming data-safety banner appears over something that never happened.

### 4.9 `project/AudioManager/Code/Doer/MusicIntegrator.cs` - `WriteExecutionRecord` (recommended, justified)

The GUI-side change is the primary ask; this is a small, contained C# improvement that materially reduces how often the strict path fires spuriously. **Do implement it.**

Inside the existing `catch`, before printing, attempt exactly one retry to an alternate path `Path.Combine(Constants.LogsPath, $"execution-{timestamp}-retry.json")` inside its own nested `try`. Rationale: the dominant real-world cause of this catch firing is transient - an antivirus or indexer holding the just-created file - and a retry to a different filename costs nothing on the success path.

- On retry success: print `\n  EXECUTION JSON: {retryPath}` (the **same** marker the GUI matches on, so no Python change is needed) and swallow the original exception.
- On retry failure: print `\n  [ERROR] EXECUTION RECORD FAILED: {ex.Message}` - promoted from `[WARN]` to `[ERROR]` and reworded so it is greppable and so the console reader sees it as the serious event it now is.

**Do not change the process exit code.** A successful integration whose record failed to write must still exit 0; making it exit non-zero would route the GUI into `result.ok == False` and fire an error modal claiming the integration failed, which is a different false statement. The GUI already treats the missing record as a hard stop on its own, which is the correct place for that judgment. Do not throw from `WriteExecutionRecord` - the original contract's rule that a failed record must never fail a run that already moved files still holds.

The GUI is deliberately **not** taught to parse the new `[ERROR] EXECUTION RECORD FAILED` line. Parsing console text to decide how to report a run is exactly what this work removes; the absent record is sufficient signal. The line is for a human reading `gui/.cache/run-logs/`.

### 4.10 Docs

- Update the module docstring's stage-4 paragraph in `gui/tabs/integration.py` to state that a real run with no readable execution record is a terminal verification failure, not a degraded success.
- Add one row or short paragraph to `docs/References/Execution-Record-Contract-Design.md` section 4.5 noting that its "when `record` is None, keep today's behaviour + unverified sentence" instruction is **superseded by this document**. Do not rewrite that doc; a single cross-reference line is enough, and its historical record of what shipped first has value.
- No new doc file beyond this one.

---

## 5. Tests

### 5.1 Existing tests in `gui/tests/test_integration.py` that must change

Every one of these currently exercises a real-run path with no execution record and therefore asserts the fallback that is being deleted. Line numbers are as of commit `883faeae`.

| Test | Line | Action |
| --- | --- | --- |
| `test_finish_execute_falls_back_to_local_counts_when_confidence_report_absent` | 405 | **Delete.** It asserts the deleted fallback outright; there is nothing left to adapt it to. |
| `test_finish_execute_none_record_sets_exec_record_missing_and_appends_unverified_sentence` | 457 | **Rewrite** into the new hard-stop assertions (5.2 item 1). Rename to `..._sets_all_statuses_unknown_and_reports_verification_failed`. |
| `test_failed_filename_from_output_extracts_name_after_prefix` | 267 | **Delete** with the function (4.5). |
| `test_failed_filename_from_output_returns_none_when_absent` | 272 | **Delete** with the function (4.5). |
| `test_run_execute_on_exe_failure_marks_named_file_failed_others_notrun` | 276 | **Rewrite.** Its whole premise (name the failed file from console output) is gone. Assert instead that all statuses are `unknown` and `exec_record_fail_reason == "missing"`. Rename accordingly. |
| `test_run_execute_on_failure_refreshes_before_opening_error_modal` | 314 | **Keep, adjust.** The refresh-before-modal ordering is unchanged; only fix any status assertion it makes. |
| `test_run_execute_on_cancel_marks_everything_notrun_not_failed` | 527 | **Rewrite** to `..._marks_everything_unknown_not_notrun`, asserting `exec_record_fail_reason == "cancelled"` and the cancelled wording. |
| `test_run_execute_on_cancel_triggers_cancelled_modal_not_error_modal` | 553 | **Keep, adjust.** Modal choice is unchanged; fix status assertions only. |
| `test_run_execute_on_non_cancelled_failure_still_uses_error_modal` | 587 | **Keep, adjust.** Same. |
| `test_run_execute_on_success_shows_no_modal_at_all` | 621 | **Keep, adjust.** A successful run with no record still shows no modal (the banner and gate are not a modal) - assert that explicitly, plus `unknown` statuses. |
| `test_finish_execute_sets_exec_ok_true_on_success` | 351 | **Keep, adjust** if it asserts a summary string; `exec_ok` semantics are unchanged. |

`test_finish_execute_record_maps_moved_skipped_error_and_absent_to_notrun` (421), `test_finish_execute_unrecognized_status_flags_only_that_row` (473), `test_run_execute_simulated_populates_confidence_report_without_console_reparsing` (506) and every `test_update_exec_status_*` (701-751) must pass **unchanged** - they cover the record-present path and the live in-flight parser, neither of which this change alters. If any of them break, the implementer has over-reached: stop and re-read section 4.3.

Nothing in `gui/tests/test_routing.py` changes. `gui/routing.py` is not touched by this design.

### 5.2 New tests in `gui/tests/test_integration.py`

1. `_finish_execute(result_ok, None)` sets every `exec_status` value to `"unknown"`, sets `exec_record_missing` True, and `exec_summary` contains `"VERIFICATION FAILED"` and does **not** contain `"Integration complete"` or any digit-plus-`" moved"` count.
2. A previously `done` status written by `_update_exec_status` during the run is **overwritten** to `unknown` when the record is absent - assert by pre-seeding `S.exec_status` with `done`/`skipped` values before calling `_finish_execute(result, None)`.
3. `run_execute` with a runner whose output contains no `EXECUTION JSON:` marker and no matching file sets `exec_record_fail_reason == "missing"` and `exec_record_path == ""`.
4. `run_execute` where `execution_record_path` resolves a path but `parse_execution_record` raises `SchemaVersionError` sets `exec_record_fail_reason == "unreadable"` and `exec_record_path` to that path, and the summary says `"could not be read"`. Monkeypatch `routing.parse_execution_record`.
5. Same as 4 but raising `json.JSONDecodeError` - confirms the whole exception tuple routes to `unreadable`, not just one member.
6. A cancelled run sets `exec_record_fail_reason == "cancelled"`, summary contains `"Run cancelled"` and **not** `"VERIFICATION FAILED"`, and every status is still `unknown`.
7. On a not-ok, non-cancelled run with no record, `exec_summary` contains **both** `result.interpreted("Integration")`'s text and the verification-failed sentence, in that order.
8. `exec_unknown_ack` defaults False after a record-less run, and `_run_analysis_now()` is a no-op (stage stays 4, no scan task created) while it is False - and proceeds once it is True. Monkeypatch `run_scan`.
9. `EXEC_STATUS_LABELS` has a key for every status `_finish_execute` and `_update_exec_status` can produce, `"unknown"` included - a table-driven test so a future status can never render a `KeyError` in `stage_execute`.
10. A successful run **with** a valid record leaves `exec_record_missing` False, `exec_record_fail_reason == ""`, and produces the normal `"Integration complete"` summary with counts - the regression guard that the strict path did not leak into the happy path.

### 5.3 C# tests

New cases in the existing execution-record test file (whichever `MusicIntegrator*Tests.cs` currently covers `WriteExecutionRecord`; if none does, add them to `MusicIntegratorExecutionRecordTests.cs`). Any new test **file** must be registered in BOTH `AudioManager.csproj` (`<Compile Include>`) and the hardcoded type array in `Tests/TestRunner.cs`, and the new test names must be confirmed present in `--test` output before claiming done.

11. The retry path writes to `execution-{timestamp}-retry.json` and prints the standard `EXECUTION JSON:` marker when the primary write fails. If `WriteExecutionRecord` is not currently reachable from a test (it is a private instance method), assert on the extracted-and-made-internal helper rather than reflecting - and if extraction turns out to require restructuring anything beyond `WriteExecutionRecord` itself, **skip this test and say so in the commit message** rather than refactoring the class.
12. A total failure prints a line containing `[ERROR] EXECUTION RECORD FAILED` and does not throw.

### 5.4 Gate

Run `scripts/dev/verify.bat` and confirm both suites green before reporting done. Report the C# and Python test counts; a count that dropped without a matching deletion in section 5.1 means a test file silently fell out of the build.

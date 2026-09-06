# Execution-Record Contract: Design Decision

**Status: design settled, ready for mechanical Sonnet implementation.** Research and evidence: `docs/References/Execution-Record-Contract-Research.md` (read that first; it is not repeated here). Backlog items closed by implementing this: IDEAS.md "[OPUS] Real-integration outcomes are reconstructed from console prose" and "[OPUS] Contract versioning is inverted relative to risk". Partially addresses "[OPUS] Hand-rolled JSON on both sides of the safety-critical manifest boundary" (the `JStr` half only - see section 6).

The implementer follows this document as a checklist and makes no judgment calls. If any instruction here appears to require touching something section 1 marks as forbidden, **stop and escalate rather than improvising**.

---

## 1. Escalation-boundary ruling (read before anything else)

**Ruling: NO - this change does not fall inside the guarded path, and the guarded set is NOT being grown.** CLAUDE.md's boundary is "the path from a decline click to the exe's argument list - `IntegrationState.accepted`/`declined`, the manifest-writing block in `run_execute`, and the construction of `args`", justified as the only thing standing between a declined file and it being moved for real. This design changes only how the GUI *reports* what already happened, after the exe has exited. It cannot cause a declined file to be moved. CLAUDE.md also states that growing the guarded set always needs approval, and no such approval exists in this session, so ruling otherwise would itself be out of bounds.

The design is deliberately shaped to keep that ruling true, which produces the single hardest constraint in this document:

**FORBIDDEN, no exceptions: the implementer must not modify `_write_manifest`, `IntegrationState.accepted`, `IntegrationState.declined`, or the `args` list returned by `_write_manifest` and passed to `runner.run`.** In particular, do **not** add `--json-output` (or any other flag) to the real-run argument list to make the exe emit the execution record. The C# side emits it unconditionally on a real run instead (section 3.3), precisely so the argument list stays byte-for-byte unchanged. If you conclude the record cannot be produced without changing `args`, stop and escalate; do not change `args`.

Two things are nevertheless **sensitive** and must be reviewed by David before shipping, as a condition of *this changeset* rather than as a permanent boundary expansion:

| Sensitive (review before ship) | Freely editable |
| --- | --- |
| `gui/tabs/integration.py`: `_finish_execute` (it now decides each track's final displayed fate) | `gui/tabs/integration.py`: `_update_exec_status` docstring/demotion, `stage_execute` and any display/label code |
| `gui/routing.py`: the new `parse_execution_record` and the version check | `gui/routing.py`: docstring, `parse_projected_libchecker` (untouched) |
| `MusicIntegrator.cs`: the new `WriteExecutionRecord` call site in `PrintRoutingResultsAndFinish`, and `PrintConfidenceReport`'s refactor into compute-then-print | `MusicIntegrator.cs`: `BuildJson` field additions, `JStr`, all new tests |
| Anything under the FORBIDDEN list above | All test files, both languages |

Rationale for the sensitive column: a wrong mapping in `_finish_execute` or a silently-dropped record produces a confident, wrong account of irreversible file moves - the same *class* of harm as the guarded path, arriving by a different route, even though it cannot itself move a file.

---

## 2. Contract shape decision

**Decision: one builder, one schema, two record types, two filenames.**

- Reuse `BuildJson` for both. Do **not** invent a second serializer.
- Extend the shape with four new top-level keys (`schemaVersion`, `recordType`, `dryRun`, `generatedAt`), one new per-file key (`detail`), and one new top-level block (`confidence`, null on dry runs).
- **Distinct filenames.** Dry run keeps `logs/routing-{timestamp}.json`. A real run writes `logs/execution-{timestamp}.json`. Reason: `routing_path_from_output` falls back to "newest `routing-*.json` in `logs/`" when the marker line is missing, so sharing one filename pattern makes it possible for the GUI to read a stale dry-run file as the real outcome - exactly the silent-wrong-account failure this work exists to remove. Relying on `status` values ("would-move" vs "moved") to tell them apart is not sufficient, because the failure mode is reading the *wrong file*, not misreading the right one. `recordType` + `dryRun` are belt-and-braces on top of the filename split.

### 2.1 Literal shape (real run, `logs/execution-{timestamp}.json`)

```json
{
  "schemaVersion": 1,
  "recordType": "execution",
  "dryRun": false,
  "generatedAt": "2026-09-06T14:03:11",
  "summary": {
    "routes": { "Artists": 12, "Misc": 3 },
    "miscAutoMigrations": [ { "artist": "Dave", "count": 4 } ],
    "miscAutoMigrationTotal": 4,
    "compilationAlbums": [ "Now 87" ]
  },
  "confidence": {
    "countCheck": {
      "totalFiles": 15,
      "moved": 12,
      "skipped": 3,
      "expectedMoved": 12,
      "ok": true
    },
    "sanityCheck": {
      "ran": true,
      "ok": true,
      "checked": 12,
      "failures": [ { "destination": "Artists/Dave/Singles/Dave - Titanium.mp3", "reason": "missing" } ]
    },
    "newFolders": [ "Artists/Dave/Singles" ],
    "errorCount": 0,
    "errors": [ { "filename": "Foo - Bar.mp3", "detail": "Access to the path is denied." } ]
  },
  "files": [
    {
      "filename": "Dave - Titanium.mp3",
      "artist": "Dave",
      "title": "Titanium",
      "album": "Psychodrama",
      "destination": "Artists/Dave/Singles/Dave - Titanium.mp3",
      "reason": "3+ songs this batch",
      "isNewFolder": true,
      "status": "moved",
      "detail": "",
      "inBatchDuplicate": false,
      "compilationAlbum": false,
      "libraryDuplicate": false,
      "dupLibraryPath": "",
      "dupLibraryTrack": "",
      "dupLibraryAlbum": "",
      "dupNewAlbum": "",
      "dupRecommendationKey": "",
      "dupRecommendation": "",
      "dupReason": "",
      "tagChanges": []
    }
  ]
}
```

### 2.2 Types (exact, non-negotiable)

| Key | Type | Notes |
| --- | --- | --- |
| `schemaVersion` | int | Always `1` for this change. |
| `recordType` | string | `"execution"` on a real run, `"routing"` on a dry run. No other values. |
| `dryRun` | bool | Redundant with `recordType` on purpose; both are checked. |
| `generatedAt` | string | `DateTime.Now.ToString("s", CultureInfo.InvariantCulture)` - identical convention to `StatsJson.cs` line 114. |
| `summary` | object | Unchanged from today, all four keys always present. |
| `confidence` | object or `null` | `null` on a dry run (the confidence report's sanity check never runs there). Object on a real run. |
| `confidence.countCheck.totalFiles` / `.moved` / `.skipped` / `.expectedMoved` | int | Exactly the four values `PrintConfidenceReport` already computes (`totalFiles`, `movedCount`, `skippedCount`, `totalFiles - skippedCount`). |
| `confidence.countCheck.ok` | bool | The existing `countOk`. |
| `confidence.sanityCheck.ran` | bool | `true` on a real run, i.e. whenever `confidence` is non-null. |
| `confidence.sanityCheck.ok` | bool | `false` iff `failures` is non-empty. |
| `confidence.sanityCheck.checked` | int | Count of entries with `status == "moved"` and a non-empty destination that were re-read. |
| `confidence.sanityCheck.failures[]` | array of objects | `{"destination": string, "reason": "missing" \| "unreadable"}`. Empty array when clean - never omitted, never null. |
| `confidence.newFolders[]` | array of string | The existing `newFolders` set, sorted ordinal-ignore-case for determinism. Empty array allowed. |
| `confidence.errorCount` | int | `errors.Length`. Emitted separately so a consumer never has to trust array length against a stated count silently disagreeing. |
| `confidence.errors[]` | array of objects | `{"filename": string, "detail": string}`, one per entry with `status == "error"`. |
| `files[].detail` | string | New. `LogEntry.Detail`, `""` when null. This is the error text the GUI currently has no structured access to. |
| `files[]` all other keys | unchanged | Same names, same types, same order as today. |

Nothing is removed. Every existing key keeps its exact current name and type, so the change is purely additive on the emitter side.

### 2.3 What this retires

Both `gui/routing.py:parse_confidence_report` and `gui/tabs/integration.py:_update_exec_status`'s authority over final status. `parse_confidence_report` is **deleted** (section 4.4). `_update_exec_status` is **kept but demoted** to live in-flight progress only (section 4.5) - deleting it would remove per-track feedback while the exe runs, which the record cannot provide because it does not exist until the exe exits.

`parse_projected_libchecker` is out of scope and must not be touched: it is dry-run-only and belongs to a different backlog item.

---

## 3. C# changes (`project/AudioManager/Code/Doer/MusicIntegrator.cs` unless stated)

### 3.1 `BuildJson` - additive

- Add a `const int SchemaVersion = 1;` private static field on `MusicIntegrator` (mirror `StatsJson`'s naming).
- Change the signature to `internal static string BuildJson(List<LogEntry> entries, BatchSummary summary = null, bool dryRun = true, ConfidenceRecord confidence = null)`. Defaulting `dryRun` to `true` and `confidence` to `null` keeps every existing test call site compiling unchanged.
- Emit, in this order, immediately after the opening `{`: `schemaVersion`, `recordType` (`dryRun ? "routing" : "execution"`), `dryRun`, `generatedAt`, then the existing `summary` block, then the new `confidence` block, then the existing `files` array.
- `confidence` emits the literal `null` when the parameter is null; otherwise the object in section 2.1.
- In the per-file loop, add `"detail": {JStr(e.Detail ?? "")}` immediately after the `"status"` line.

### 3.2 New `ConfidenceRecord` type

Add a small internal class (same file, beside `LogEntry`) holding exactly the section-2.2 confidence fields: `TotalFiles`, `Moved`, `Skipped`, `ExpectedMoved`, `CountOk`, `SanityRan`, `SanityChecked`, `List<(string Destination, string Reason)> SanityFailures` (or a two-field nested class - implementer's choice, it is not observable), `List<string> NewFolders`, `List<(string Filename, string Detail)> Errors`. No behaviour, data only.

### 3.3 `PrintConfidenceReport` - compute then print, then write

- Change it to `private ConfidenceRecord PrintConfidenceReport(List<LogEntry> entries, int totalFiles, int movedCount, int skippedCount)`.
- **Do not change a single line of console output text.** Existing Python tests and users' expectations depend on it; the point of this change is to stop *depending* on that text, not to alter it. Populate the `ConfidenceRecord` from the same locals the existing prints already use (`countOk`, `newFolders`, `failedSanity`, `errors`) and return it. `failedSanity` currently stores pre-formatted `"  [MISSING] {dest}"` strings; capture the destination and the reason (`"missing"` / `"unreadable"`) into the record at the same two points where those strings are built, rather than parsing them back out.

### 3.4 New `WriteExecutionRecord`

Add beside `WriteJsonOutput`, same structure and the same `try/catch` that prints `  [WARN] ...` rather than throwing (a failed record must never fail an integration run that already moved files):

- Path: `Path.Combine(Constants.LogsPath, $"execution-{timestamp}.json")`, timestamp format `yyyyMMdd-HHmmss` - identical to `WriteJsonOutput`.
- Body: `BuildJson(entries, summary, dryRun: false, confidence: confidence)`, where `summary` is built exactly as `WriteJsonOutput` builds it today (`_miscMigrationCandidates`, `_compilationAlbums`).
- Prints `\n  EXECUTION JSON: {path}` on success and `\n  [WARN] Execution record failed: {ex.Message}` on failure.
- Factor the shared body/summary construction out of `WriteJsonOutput` if convenient, but do not change `WriteJsonOutput`'s own path, its printed `  JSON: {path}` line, or its dry-run gating.

### 3.5 Call site - `PrintRoutingResultsAndFinish` (~line 753)

Replace:

```
if (!dryRun)
    PrintConfidenceReport(logEntries, totalFiles, movedCount, skippedCount);
```

with a call that captures the returned `ConfidenceRecord` and, still inside the same `if (!dryRun)`, immediately calls `WriteExecutionRecord(logEntries, record)`. The record **must** be written after `PrintConfidenceReport` (it needs the sanity-check results) and **before** `RunMiscMigration()`/`CleanupNewMusicFolder()`.

**Unconditional on a real run - not gated on `jsonOutput`.** This is what keeps the GUI's `args` untouched (section 1). The `jsonOutput` flag continues to gate the dry-run `WriteJsonOutput` call at line 232 and is otherwise unchanged.

Known and accepted scope limit, to be recorded in the commit message and not "fixed" by the implementer: Misc auto-migration and folder cleanup run *after* the record is written and move library files that are not in `logEntries`, so the record does not cover them. That is the pre-existing situation and a separate backlog concern.

### 3.6 `JStr` - fix in this pass (see section 6)

Extend `MusicIntegrator.JStr` to escape the full C0 range: keep the existing `\\` and `\"` handling, map `\b`, `\f`, `\n`, `\r`, `\t` to their short escapes, and emit any remaining character below U+0020 as `\u00XX`. Stop stripping `\r` silently - emit `\\r`. Change **only** this copy; `StatsJson`'s and `TracksJson`'s copies stay as they are and remain covered by the separate Newtonsoft backlog item.

### 3.7 Docs

Update the XML doc comment above `BuildJson` and `WriteJsonOutput` to describe the new keys and the two filenames. Add a short "Execution record" row/paragraph wherever `docs/References/` already documents the routing JSON contract, if such a section exists; do not create a new doc file.

---

## 4. Python changes

### 4.1 `gui/routing.py` - version constant and check

- Add `ROUTING_SCHEMA_VERSION = 1` at module level.
- Import `SchemaVersionError` from `gui.data_loader` and reuse it verbatim - do not define a second exception class. Verify there is no import cycle (`data_loader` must not import `routing`); if one exists, stop and escalate rather than restructuring.
- Add a private `_check_version(raw, path)` that raises `SchemaVersionError(path.name, found, ROUTING_SCHEMA_VERSION)` when `raw` is not a dict or `raw.get("schemaVersion") != ROUTING_SCHEMA_VERSION`. Same semantics as `data_loader._check_schema`.
- Call it at the top of `parse_routing_document`, `parse_routing_file`, `parse_batch_summary`, and the new `parse_execution_record`.

### 4.2 Versioning migration decision - hard fail, no leniency

**Decision: hard-fail on missing or mismatched `schemaVersion`, and delete `_file_rows`'s bare-top-level-array acceptance entirely.** Once a version field exists, silently accepting an unversioned file is precisely the "silent mis-read" `data_loader`'s docstring already calls worse than a crash, and the review's "versioning is inverted relative to risk" finding is about this exact leniency.

Concrete migration behaviour:
- Old `routing-*.json` files already sitting in `logs/` become unreadable by the GUI and raise `SchemaVersionError` with the existing clear message. **This is accepted and intended.** They are disposable per-run artifacts, not user data; the Integration tab always writes a fresh one at the start of a batch. No converter is written and no fallback path is added.
- `_file_rows` becomes dict-only: return `raw["files"]` if it is a list, else `[]`. Delete the `isinstance(raw, list)` branch and the module-docstring paragraph describing it (lines 12-14).
- Additionally tighten `routing_path_from_output`'s mtime fallback so a stale file cannot be picked up: it takes a new required `since: float` argument (a `time.time()` captured by the caller *before* the exe is launched) and the `glob` fallback only considers files with `st_mtime >= since`. Returns `None` when nothing qualifies. Update its one caller accordingly.

### 4.3 `gui/routing.py` - new `parse_execution_record`

Add:

- `def parse_execution_record(path: Path) -> dict` - reads the file, runs `_check_version`, and additionally raises `SchemaVersionError` (same class, `found` = the offending value) if `recordType != "execution"` or `dryRun` is not `False`. A dry-run document must never be interpretable as a real outcome.
- Returns `{"generatedAt": str, "summary": <as today>, "confidence": <parsed or None>, "files": <_parse_entries output, now including "detail">}`.
- `_parse_entries` gains `"detail": e.get("detail") or ""`.
- A private `_parse_confidence(raw)` builds, defensively and always with the full key set (same discipline as `_parse_summary`): `count_ok`, `sanity_ok`, `sanity_ran`, `sanity_checked`, `sanity_failures` (list of `{"destination", "reason"}`), `new_folders`, `error_count`, `errors` (list of `{"filename", "detail"}`), `total_count`, `moved_count`, `skipped_count`. Returns `None` when the `confidence` key is `null`/absent. Keep the `total_count`/`moved_count`/`skipped_count` key names so `_finish_execute`'s existing summary code reads them unchanged.
- Add `def execution_record_path(lines: list[str], since: float) -> Path | None` mirroring `routing_path_from_output`: primary match on the literal `EXECUTION JSON: <path>` marker, fallback to the newest `execution-*.json` in `config.LOGS_DIR` with `st_mtime >= since`, else `None`.

### 4.4 Delete `parse_confidence_report`

Remove the function and its docstring from `gui/routing.py`. Remove its tests from `gui/tests/test_routing.py`. Grep the whole repo for `parse_confidence_report` and confirm zero remaining references before finishing.

### 4.5 `gui/tabs/integration.py`

- `run_execute`: capture `started = time.time()` immediately before `await runner.run(...)`. After the run, resolve `path = routing.execution_record_path(result.lines, started)` and, if non-None, `record = routing.parse_execution_record(path)` inside a `try` catching `(OSError, ValueError, json.JSONDecodeError, SchemaVersionError)` and setting `record = None` on failure. Pass it: `_finish_execute(result, record)`. **Nothing above the `runner.run` call changes** - `targets`, `_write_manifest`, `args` are untouched (section 1).
- `_finish_execute(result: RunResult, record: dict | None = None)`:
  - `S.confidence_report = record["confidence"] if record else None`. The downstream summary code reading `moved_count`/`skipped_count` keeps working as-is.
  - Add `S.exec_record_missing = record is None` to `IntegrationState` (default `False`) so the UI can say so.
  - **When `record` is present and `result.ok`:** overwrite every entry in `S.exec_status` from the record's `files`, matched on `filename`, using exactly this mapping - `moved` -> `done`, `skipped` -> `skipped`, `error` -> `failed`. Any target filename absent from the record -> `notrun`. Any record status not in that mapping (e.g. `would-move`) -> treat the whole record as untrustworthy: discard it, set `record = None`, and fall through to the branch below. Do not guess.
  - **When `record` is None:** keep today's behaviour exactly (the sweep of `queued`/`moving` -> `done`, the `_failed_filename_from_output` path on failure), and append to `S.exec_summary` the sentence `"No execution record was found - these outcomes are from console output and are unverified."` Never silently present unverified statuses as verified.
    - **Superseded by `Execution-Record-Fallback-Removal-Design.md`:** the console-output fallback described in this bullet was removed - a missing or unreadable record is now a terminal verification failure (every status forced to `"unknown"`), not a degraded success. See that document for the shipped behaviour.
  - On `not result.ok`, still consume the record if one exists (a run can fail after moving some files, and the record is then the best account available); statuses come from the record, and the existing `result.interpreted(...)`/modal behaviour is unchanged.
- `_update_exec_status`: unchanged logic, but update the docstring to state it is now **live in-flight progress only** and that `_finish_execute` overwrites every status from the execution record when one is available. Do not delete it.
- `run_execute_simulated`: stop relying on the synthetic `CONFIDENCE REPORT` console lines being re-parsed. Build a synthetic record dict in Python with the same shape `parse_execution_record` returns (`recordType` semantics already satisfied by construction) - one `files` row per target with `status: "moved"`, and a `confidence` block with `total_count`/`moved_count`/`skipped_count` set and `count_ok`/`sanity_ok` true - and pass it to `_finish_execute(result, record)`. Keep emitting the synthetic console lines too, so `_update_exec_status`'s live path is still exercised.
- `stage_execute` / display code: surface `S.exec_record_missing` as a visible warning row. Free to style as you like.
- Update the module docstring's stage table (line 3 area) to name the execution record as the stage-4 artifact.

---

## 5. Tests

### C# (`project/AudioManager/Code/Tests/`)

New file `MusicIntegratorExecutionRecordTests.cs`. **It must be registered in BOTH `AudioManager.csproj` (`<Compile Include>`) and the hardcoded type array in `Tests/TestRunner.cs`** - IDEAS.md already records that missing either silently shrinks the suite while still reporting `[PASS]`. Verify the new test names actually appear in `--test` output before claiming done.

Cover:
1. `BuildJson` with `dryRun: true, confidence: null` emits `"schemaVersion": 1`, `"recordType": "routing"`, `"dryRun": true`, `"confidence": null`.
2. `BuildJson` with `dryRun: false` and a populated `ConfidenceRecord` emits `"recordType": "execution"` and every section-2.2 confidence key.
3. `detail` is emitted for an error entry and is `""` (not `null`) for a null `Detail`.
4. Round-trip validity: the output of a populated `BuildJson` parses as JSON. If no JSON parser is available in the test project, assert brace/bracket balance and that no raw control character below U+0020 appears outside an escape.
5. `JStr` escapes tab, `\b`, `\f`, `\r` (as `\\r`, not stripped) and a bare U+0001 control character as the six-character sequence backslash-u-0-0-0-1 (assert on the escaped text, and note the C0 escape form is spelled out here rather than shown literally so it survives copy-paste).
6. Existing `MusicIntegratorBatchSummaryTests` / `MusicIntegratorDuplicateJsonTests` must still pass unchanged - if they assert on exact full-document text, update those assertions to include the new keys, but do not weaken them to substring checks that would no longer catch a shape change.

### Python (`gui/tests/`)

In `test_routing.py`:
7. `parse_execution_record` on a valid fixture returns the full parsed shape including `detail` and every confidence key.
8. Missing `schemaVersion` raises `SchemaVersionError`; `schemaVersion: 2` raises `SchemaVersionError`; a bare top-level array now raises `SchemaVersionError` (this replaces the old "bare array is accepted" test - delete that one).
9. A document with `recordType: "routing"` / `dryRun: true` passed to `parse_execution_record` raises `SchemaVersionError`.
10. `execution_record_path` prefers the marker line; falls back to the newest qualifying file; returns `None` when the only candidate predates `since`.
11. Malformed `confidence` sub-values (string where int expected, null arrays) yield the full defaulted key set rather than raising.

In `test_integration.py`:
12. `_finish_execute` with a record maps `moved`/`skipped`/`error` to `done`/`skipped`/`failed`, and a target absent from the record to `notrun`.
13. `_finish_execute` with `record=None` reproduces today's behaviour **and** appends the unverified-outcomes sentence and sets `exec_record_missing`.
14. A record containing a `would-move` status causes the record to be discarded and the unverified path taken.
15. `run_execute_simulated` still ends with correct statuses and a populated `S.confidence_report` with no console re-parsing.
16. Any existing test asserting `parse_confidence_report` behaviour is deleted, not adapted.

Also add a fixture execution record under `gui/tests/fixtures/`. Finally, run `scripts/dev/verify.bat` and confirm both suites are green before reporting done.

---

## 6. `JStr` escaper gap - fix now, minimally

**Decision: fix `MusicIntegrator.JStr` in this same pass (section 3.6). Defer the Newtonsoft migration.**

Reasoning. Today the gap is dry-run-only: a malformed routing JSON costs a re-scan, which is annoying and visible. After this change the same escaper produces the *only* durable account of irreversible file moves, and the values most likely to contain a tab or other C0 character are exactly the new ones - `detail`, which carries raw exception text, and `errors[].detail`. The failure would also be maximally quiet: the exe writes the file, prints its path, and exits 0; the GUI's `json.load` raises, the record is treated as missing, and the user gets the unverified-console fallback for the run that most needed a record - the one that errored. Fixing the escaper is a handful of lines in one private static method with a unit test, so the cost is close to zero against that.

Replacing all three hand-rolled emitters and `IntegrationManifest.Parse` with Newtonsoft is a genuinely larger change that touches the manifest parser - which *is* inside the guarded path - and it stays on its own IDEAS.md line. Do not start it here.

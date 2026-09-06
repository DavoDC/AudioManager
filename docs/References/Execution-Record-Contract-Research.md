# Execution-Record Contract: Research Findings (for Opus design pass)

**Status: Research only. No design decision made here - the design step (contract shape, versioning, how it interacts with the escalation boundary) is Opus-level per CLAUDE.md's "Opus Orchestrates, Sonnet Implements" rule and has not happened yet.** This doc exists so that design pass starts from evidence instead of re-discovering these facts from zero.

Backlog item this feeds: `docs/Development/IDEAS.md` - "[OPUS] Real-integration outcomes are reconstructed from console prose, not a data contract."

## The key finding: the fix is smaller than it looks

The problem statement (GUI reconstructs real-integration outcomes by regexing `MusicIntegrator.cs`'s `Console.WriteLine` text) is accurate, but the natural next assumption - "we need to design a brand-new JSON contract from scratch" - is wrong. The structured data already exists in memory on the C# side, and a tested JSON serializer for it already exists and already runs today, just not for real runs.

## What already exists (C# side, `project/AudioManager/Code/Doer/MusicIntegrator.cs`)

- **`LogEntry`** (class def ~line 48-70): one instance per file, already carries `Filename`, `Title`, `Artists`, `Album`, `Destination`, `Reason`, `TagChanges`, `Status` ("moved"/"would-move"/"skipped"/"error"), `Detail`, `IsNewFolder`, `InBatchDuplicate`, `CompilationAlbum`, plus a library-duplicate block (`LibraryDuplicate`, `DupLibraryPath`, `DupLibraryTrack`, `DupLibraryAlbum`, `DupNewAlbum`, `DupRecommendationKey`, `DupRecommendation`, `DupReason`).
- **`logEntries`** - a `List<LogEntry>` built during both dry-run and real routing (`RouteAllFiles`, ~line 421-534) and passed through to `PrintConfidenceReport` (~line 1009) at the end of a run, real or dry.
- **`BuildJson(entries, summary)`** (~line 896-935) - already serializes the full `LogEntry` list to JSON (`files` array with every field above) plus a `summary` block (route distribution, Misc auto-migration counts, compilation albums, via `AppendSummaryBlock` ~line 943-972). This is a hand-rolled `StringBuilder`/`JStr`-escaped serializer (see "known risk" below), but it is the SAME code path `gui/routing.py` already parses today for dry runs - it works and is exercised in production.
- **`WriteJsonOutput(entries)`** (~line 974-996) writes `BuildJson`'s output to `logs/routing-{timestamp}.json` and prints `  JSON: {path}` to stdout.

**The gap:** at line 231-233,
```csharp
// Write JSON output if requested (dry-run only)
if (dryRun && jsonOutput)
    WriteJsonOutput(logEntries);
```
`WriteJsonOutput` only runs `if (dryRun && jsonOutput)`. For a REAL run, the exact same `logEntries` list - same per-file `Status`/`Destination`/`Reason`/error `Detail` - is built and handed to `PrintConfidenceReport`, which only prints it as text, then the list falls out of scope and is discarded. Nothing structured survives a real run today.

**`PrintConfidenceReport`** (~line 1009-1103) - the count check ("Files in NewMusic: N | Moved: M | Skipped: S"), the per-file `[STATUS] filename` table, new-folders-created list, destination sanity re-check (re-reads every moved file with TagLib, flags `[MISSING]`/`[UNREADABLE]`), and the final error summary (`[ERRORS: N]` + one line per error) are ALL console-only - none of this is in `BuildJson`'s current shape. If the real-run execution record is meant to also carry the confidence-report data (recommended - see "Why this matters" in IDEAS.md, `gui/routing.py`'s `parse_confidence_report` currently regexes this from console text), the JSON shape needs new fields for at least: count-check pass/fail, sanity-check pass/fail + which destinations failed, and error count/list. This part is NOT already solved - it needs actual design.

## What already exists (Python side)

- **`gui/routing.py`** already has a full parser for `BuildJson`'s exact shape: `parse_routing_document`, `_parse_entries`, `_parse_summary`, `parse_batch_summary` (lines 1-150). It is defensive (drops malformed entries, defaults missing fields) but has **no schema-version check** - a bare top-level array (pre-summary shape) is silently accepted as "no summary" rather than being flagged as an old/different format (see `_file_rows`, line 57-65, and the module docstring's own admission of this at line 12-14).
- **`gui/data_loader.py`** already has the versioning pattern to copy: `SchemaVersionError`, `_check_schema(data, path, expected)` (lines 24-46) - used today for `analysis-stats.json` and `tracks.json`. Reusing this exact pattern (add a `schemaVersion` key to `BuildJson`'s output, add a Python-side expected-version constant + check) is the natural fix for the "contract versioning is inverted relative to risk" finding.
- **`gui/tabs/integration.py`**: `_update_exec_status` (line 1132-1175) is the console-text-substring matcher for real-run per-track status (`[AUTO]`/`[SKIP]`/`Error processing file:` lines). `_write_manifest` (line 878-912) is the guarded-path manifest writer (`IntegrationState.accepted`/`declined` -> `args`). `run_execute`/`_finish_execute` (line 939-1067ish) is where `routing.parse_confidence_report(result.lines)` gets called after a real run finishes (line 1010) - this is the console-text confidence-report parse the review flagged.

## What this means for the design step (Opus's job, not covered here)

Because the emitter already exists and already works for dry runs, the design decision is narrower than "invent a JSON execution-record format." It's closer to:
1. Should real runs simply call `WriteJsonOutput(logEntries)` too (reusing `BuildJson` as-is, `Status` values already distinguish "moved"/"skipped"/"error"), or does the real-run record need a distinct filename/shape from the dry-run routing JSON (they currently share `logs/routing-{timestamp}.json` naming and `dryRun`/`Status="would-move"` is how a dry run's rows are told apart from a real run's `Status="moved"` today - is that distinction sufficient, or does conflating them risk the GUI reading a stale dry-run file as if it were the real outcome)?
2. What new fields does `BuildJson`/`AppendSummaryBlock` need to also carry the confidence-report data (count check, sanity check, errors) so `gui/routing.py`'s `parse_confidence_report` text-regex can be retired too - not just `_update_exec_status`'s per-track loop?
3. Version number and check: straightforward to copy `SchemaVersionError`'s pattern, but need to decide what happens to `_file_rows`'s current "bare array = old shape, still accepted" leniency once a version field exists - should an unversioned/mismatched file now hard-fail per the "contract versioning is inverted relative to risk" finding, even though that changes current lenient behavior for old dry-run routing JSON files sitting in `logs/`?
4. This will touch `gui/tabs/integration.py`'s `run_execute`/`_finish_execute`/`_update_exec_status`, which sits directly beside (arguably inside) the GUI Code-Change Escalation Boundary (`IntegrationState.accepted`/`declined`, the manifest-writing block, `args` construction) - not because this change touches the manifest/args path itself, but because it changes how the GUI decides a track's real-world fate after the exe runs, which is the same safety-critical territory. CLAUDE.md's boundary rule text should guide whether this needs the same "written unattended, reviewed before it ships" treatment even though it's a different code path than the manifest write.

## Known pre-existing risk to keep in mind while designing

`BuildJson`'s `JStr` escaper (~line 999-1003) only escapes `\`, `"`, `\r`, `\n` - a tag value containing a literal tab or other C0 control character would produce invalid JSON. This is already flagged as its own IDEAS.md backlog item ("[OPUS] Hand-rolled JSON on both sides of the safety-critical manifest boundary") - worth deciding during this design pass whether extending `BuildJson`'s real-run use makes that pre-existing risk more urgent to fix first, since a real (not dry-run) execution record failing to serialize would be a more consequential silent failure than today's dry-run-only usage.

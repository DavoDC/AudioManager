# MusicIntegrator Decomposition: Design (ARCH-4)

Status: PROPOSAL. No code has moved. David approves this document before any extraction starts.

Date: 2026-10-12. Source: `project/AudioManager/Code/Doer/MusicIntegrator.cs`, about 2,400 lines as of this date (it was 2,201 when the 2026-09-06 architecture review flagged it).

## 1. Why

`MusicIntegrator` is the one class that performs the irreversible workflow (it moves files from NewMusic into the library), and it is also the largest and least decomposed type in the program. Batch scanning, scan-ahead promotion, duplicate detection and resolution, routing, Misc auto-migration, NewMusic cleanup, routing-JSON emission, the confidence report and the projected LibChecker all live in it. Three of its four constructors exist only so tests can reach internal logic.

The aim is smaller units that can each be tested alone, with the file-moving core left thin and easy to review. It is not a rewrite: every extraction is a verbatim move of existing code behind a new class, with behaviour pinned first.

## 2. Ground rules

1. Characterisation first. Before any method moves, a test that passes on the current code pins its behaviour. If an existing test already covers it (named below), that test is the pin and a missing case is added first.
2. Verbatim moves. An extraction changes where code lives and how it is called, never what it does. No refactoring inside the same commit as a move.
3. One class per commit, each commit green on `scripts\dev\verify.bat` (unit tests, the routing manifest, and the GUI half).
4. Every move is also gated by a before/after comparison of a real `AudioManager.exe integrate --dry-run --no-input` run against the same NewMusic content: the console output and the routing JSON must be identical apart from the timestamp. Claude may run dry-run; only David runs real integration.
5. The guarded path stays guarded: nothing here changes the exe arguments the GUI builds, the `[AUTO]`, `[SKIP]` and execution-record line formats (pinned by `MusicIntegratorConsoleFormatTests`), or the JSON contract version.
6. New files are registered in `AudioManager.csproj` by hand (non-test source still needs a `<Compile Include>`); new test files need no registration (ARCH-2).
7. Stop condition: if a move cannot be made verbatim because logic is tangled across the seam, stop, record it as a finding, and ask David rather than refactoring on the fly.

## 3. Current responsibilities and the proposed classes

Line numbers are as of 2026-10-12 and will drift; the method names are the durable reference.

| Order | New class | Moves out of MusicIntegrator | Existing tests that pin it | Tests to add first |
|---|---|---|---|---|
| 0 | `IntegrationModels` (file only, no logic) | nested types `LogEntry`, `ConfidenceRecord`, `BatchSummary`, `DupData`, `ScannedFile` | all MusicIntegrator test files (compile-only pin) | none |
| 1 | `RoutingJsonWriter` (static) | `BuildJson`, `AppendSummaryBlock`, `AppendConfidenceBlock`, `JStr`, `WriteJsonOutput`, `WriteExecutionRecord`, `WriteExecutionRecordWithRetry`, `BuildCurrentSummary` | `MusicIntegratorDuplicateJsonTests`, `MusicIntegratorExecutionRecordTests`, `MusicIntegratorBatchSummaryTests`, `MusicIntegratorConsoleFormatTests` | a golden-file test of `BuildJson` for one dry-run and one real-run record |
| 2 | `ConfidenceReporter` | `PrintConfidenceReport` | covered indirectly by the execution-record tests | a test pinning the printed confidence block for a clean run and a run with failures |
| 3 | `MirrorIndex` | `BuildMirrorIndex`, `FindDuplicateInMirror`, `NormaliseTitleForDedup`, `LoadArtistAliases`, `GetAliasExpandedKeys`, `DeriveLibraryPathFromMirrorPath`, `ReadMirrorTrackInfo`, `IsAlbumFolderCompilation` | `DuplicateDetectionTests` | a test for `DeriveLibraryPathFromMirrorPath` outside the mirror root |
| 4 | `ProjectedValidator` | `BuildProjection`, `RunProjectedLibChecker`, `GetMirrorRelPath` | `ProjectedLibCheckerTests` | none expected |
| 5 | `DestinationRouter` | `GetDestDir`, `FindAlbumFolder`, `CountAlbumSongs`, `CountAkiraTheDonPersonSongs`, `CountAkiraTheDonPersonAlbumSongs`, `SanitiseFolderName`, `RunScanAhead`, `GetRouteCategory`, `GetRouteSummary` | `RoutingTests`, `RoutingFixtures`, the routing manifest (`csharp-test-fixtures/routing-manifest.json`), `RulesCharacterisationTests` | none expected; the routing suite is the strongest pin in the program |
| 6 | `DuplicateResolver` | `RunDuplicateReview`, `ExecuteDuplicateDecisions`, `PresentDuplicateAndDecide`, `BuildDupData`, `DupRecommendationText`, `MarkInBatchDuplicates`, `ReadMenuKey` | `DuplicateDetectionTests`, `MusicIntegratorDuplicateJsonTests` | a test for `ExecuteDuplicateDecisions` with K, L and D decisions against a temp library (this is the delete path, so it is pinned before it moves) |
| 7 | `NewMusicScanner` and `LibraryMaintenance` | scanner: `PreScanFiles`; maintenance: `RunMiscMigration`, `CleanupNewMusicFolder` | `MusicIntegratorRouteAllFilesTests`, `IntegrationTests` | a test for `CleanupNewMusicFolder` on a temp folder tree |

After step 7, `MusicIntegrator` keeps only: the constructors, the top-level run sequence, `RouteAllFiles` (the loop that actually moves files) and the console printing of its results (`PrintRouteDistributionPreview`, `PrintRoutingResultsAndFinish`). That remainder should be a few hundred lines that reads as the workflow, which is the part David needs to review for safety.

## 4. Why this order

Pure and low-risk first, anything that deletes or moves files last.

1. Models first so every later class can reference the types without circular nesting.
2. `RoutingJsonWriter` and `ConfidenceReporter` are output only: they cannot change where a file goes. They are the safest place to prove the extraction process and the dry-run before/after gate.
3. `MirrorIndex` and `ProjectedValidator` read the mirror and the library but write nothing.
4. `DestinationRouter` decides destinations. It is read-only, but a mistake sends files to the wrong folder, so it goes after the process is proven; the routing manifest pins it tightly.
5. `DuplicateResolver` contains the K, L and D decisions that delete files, so it moves after everything it depends on is stable and only with the new delete-path test in place.
6. The scanner and maintenance classes touch NewMusic and the Misc folder and go last for the same reason.

## 5. Constructors and test seams

Three of the four constructors exist only for tests (`MusicIntegrator(string testLibraryPath)`, with manifest, with batch album counts). As each class is extracted, its tests construct that class directly with its inputs (a library path, a mirror index, a decision), so the matching test constructor is deleted in the same commit. The public constructor and the CLI surface do not change. `InternalsVisibleTo("AudioManager.Tests")` (ARCH-2) already lets the tests reach `internal` classes, so the new classes are `internal`.

## 6. Known risks and open questions for David

1. Shared mutable state. `MusicIntegrator` keeps working state in fields (`_batchTagCache`, `_compilationAlbums`, `_lDeletedLibraryPaths`, the manifest and flags). Each extracted class must receive what it reads explicitly. If a field is both read and written across a seam, that is a design decision, not a verbatim move, and the stop condition in section 2 applies.
2. `_lDeletedLibraryPaths` links the L decision (replace library copy) to the projected LibChecker. Steps 4 and 6 must agree on who owns it; the proposal is that `DuplicateResolver` owns it and passes it to `ProjectedValidator`.
3. The known LibChecker versus GetDestDir album-grouping divergence (raw tag versus normalised tag, pinned by `Rules_LibChecker_AlbumGroupingUsesRawAlbumTag_KnownDivergence`) is not part of this work; it needs its own decision.
4. Console output is a contract with the GUI parser (`gui/routing.py`, `gui/tabs/integration.py`). The before/after dry-run gate in section 2 is what protects it; confirm you are happy with that gate in place of per-line tests for every message.
5. Effort: roughly one commit per row of section 3, so eight commits. Steps 1 to 4 are low risk and could be done in one session; 5 to 7 deserve a session each with your review between them.

## 7. Approval needed

Before any code moves, David confirms: the class list and order in section 3, the dry-run before/after gate in section 2, and whether steps 5 to 7 (routing, duplicate deletion, cleanup) should be done only with David reviewing each commit.

# History

Completed features, settled design decisions, resolved tasks, and decisions explicitly not implemented.

---

## 2026-09-05 - Review-card title color weight

Closed "[GUI] Review-card title has no more visual weight than the artist line under it" from IDEAS.md. The review card's song title (`.rc-title`) was using the same dim text color as the secondary artist line beneath it, giving no visual hierarchy to the primary identifier. Changed `.rc-title` from `color:var(--text-dim)` to `color:var(--text)` in `gui/theme.py` to match the already-bright destination route line, establishing the title as the card's lead line.

---

## 2026-09-05 - Review-card layout: width cap, visual hierarchy, declined-card treatment, keyboard path, destination sort

Closed the mechanical (`[SONNET]`) half of the "[GUI] Review-card layout" IDEAS.md item; the accept/decline interaction redesign is split out as a new `[OPUS]` item since it is a genuine design-judgment call, not a mechanical fix.

**Width and dead space.** `.review-card` capped at `max-width:820px` with the grid's decision column adjacent to the route line, eliminating the ~600-700px gap that used to separate the route/reason text from the Accept/Decline buttons pinned by the old `auto` column on a full-bleed card.

**Visual hierarchy corrected.** `.rc-route` (the destination - the actual point of the card) goes from 11px dim monospace to 14px bold `var(--text)`; `.rc-title`/`.rc-artist` are demoted to `var(--text-dim)`; badges go from 9px to 11px. The "Clean route" badge shown on every uneventful card is removed outright - absence of a badge row is now the clean signal, rather than a badge that trains the eye to skip the row where real exceptions (duplicates, new folders, errors) live.

**Declined cards get a distinct treatment**, not a uniform `opacity:.55` fade that read as "disabled" (including the Decline button's own pressed state read as greyed-out): a red `border-left`, strikethrough on the route/destination text, and an explicit "Stays in NewMusic" tag.

**Keyboard path added, scoped to review-only.** Filter chips are real `<button>`s with `:focus-visible` instead of unfocusable `<div>`s. A/D accept or decline the entry under a cursor; J/ArrowDown and K/ArrowUp move it, with a `.current` outline shown only once keyboard nav has actually been used (`S.keyboard_nav_used`), so a mouse-only reviewer never sees a surprise highlight. `_review_key_action()` is a pure key-name-to-action mapping, unit-tested directly; `_on_review_key()` self-guards on `S.stage == 2` so a stray handler can never fire outside the review screen, and ignores keyup/repeat events. The keyboard layer intentionally stops at accept/decline/navigate - the duplicate-resolution radio control stays mouse-only, confirmed still clickable and correctly positioned inside the new layout.

**Destination sort/grouping.** `sort_by_destination()` mirrors the CLI's own destination-path sort (`MusicIntegrator.cs` ~188-192, no C# change needed) so same-album files land adjacent instead of raw scan order; applied inside `IntegrationState.filtered()` so every filter view groups consistently. Filename is the tie-break.

Verified live in Simulate mode via the browser: card width/hierarchy/badge/declined-card rendering confirmed by screenshot; keyboard shortcuts confirmed with real key events (dispatched keydown, not just read from source) - J/K move the cursor and its highlight, A/D toggle accept/decline on the entry under the cursor and reflect immediately in the DOM (`declined current` class, strikethrough, tag); the library-duplicate resolution control's click-through (switching from "Delete library copy" to "Delete new file") still works inside the capped-width card. Display-only throughout: the escalation boundary (`IntegrationState.accepted`/`declined`, the manifest-writing block in `run_execute`, exe `args` construction) is untouched - confirmed via `git diff`. 44 new/updated GUI tests for `sort_by_destination`, `_review_key_action`, and `_on_review_key`'s stage-guard/cursor/keyup-repeat/error-guard behaviour; full suite green (280 C# tests, 200+ GUI tests after this and the concurrently-landed scan-ahead-context change).

---

## 2026-09-05 - Scan-ahead batch context reaches the GUI: route distribution, Misc auto-migration notice, compilation albums

Closed three `docs/Development/IDEAS.md` items. The exe had been computing batch-level scan-ahead context and printing it only to the CLI; the GUI showed none of it, so a reviewer working in the GUI made accept/decline decisions without the information the CLI user has.

**The JSON contract now carries a batch summary.** `MusicIntegrator.BuildJson` used to emit a bare top-level array of file rows. Its root is now an object, `{"summary": {...}, "files": [...]}`. The summary holds `routes` (destination category to count), `miscAutoMigrations` (per-artist counts) with a `miscAutoMigrationTotal`, and `compilationAlbums`. Each file row gained a `compilationAlbum` boolean. The route counts are derived inside `AppendSummaryBlock` from the entries' own `Destination` values rather than passed in from the pre-routing pass, so the summary and the rows the GUI renders can never disagree; entries with no destination are skipped rather than bucketed as "Unknown". `gui/routing.py` reads both shapes, so a routing JSON written by an older exe build still opens - it simply carries no summary. The full summary key set is always emitted, so no caller has to branch on key presence, only on emptiness. Ten new C# tests (`Code/Tests/MusicIntegratorBatchSummaryTests.cs`) and eleven new Python parser tests cover both root shapes, per-artist counts, zero-count filtering, JSON escaping, and rejection of malformed input.

**`compilationAlbum` records the detection, not the routing outcome.** An album can be a batch compilation (3+ distinct primary artists in this batch) and still land under `Artists/` when the primary artist already has a folder, so the flag is set from membership in `_compilationAlbums` rather than from where the file ends up. The GUI badge is shown on those cards regardless of final route.

**Three surfaces in the review stage.** A `batch-summary` strip above the confirm bar shows the route distribution (highest count first), and, when non-empty, a yellow Misc auto-migration line and a green compilation-albums line. The migration wording says "existing library song(s) will be moved out of Miscellaneous Songs" because that is the point of the notice: it announces the run moving files that are already in the library, not incoming ones. A "Compilation album" badge appears on affected cards, with a "Compilation albums (N)" filter chip added only when the count is non-zero - an always-present zero chip would be the same anti-signal as a badge meaning "nothing here". `batch_summary_html()` is a pure function so all of it is testable without a live page.

**Two smaller fixes bundled.** The confirm dialog's SIMULATED note now carries the same loud yellow as the page-level `.simulate-banner` instead of plain grey 11px `.note` text - the confirm is the one moment where confusing a simulated run with a real one matters most. And the stepper is three steps, not four: Confirm is a modal over the review stage, never a screen, so step 3 could never render as active and the stepper jumped from 2-active straight to 2+3-done. Stages 3 and 4 are both the execute screen, so they collapse onto step 3.

Display-only throughout: the escalation boundary (`IntegrationState.accepted`/`declined`, the manifest-writing block in `run_execute`, exe `args` construction) is untouched. Verified in Simulate mode with the browser. Suite green: 280 C# tests, 200 GUI tests.

---

## 2026-09-05 - Acquire tab: state persists across page reload, progress-bar maths and sort/history logic under test

Closed three related `docs/Development/IDEAS.md` items in one pass, all inside `gui/tabs/acquire.py` and
`gui/tests/test_acquire.py`. GUI suite went from 131 to 176 tests, green throughout.

**State persistence across page reload (also closes "cache fetched tracks to disk").** Fetched tracks and
the Downloaded-match state lived only in the in-memory `_state` dict, so a browser reload or a tab rebuild
came back to an empty table. `config.ACQUIRE_STATE_JSON` - which already held the last playlist ID and the
capped last-5 history - now also carries a `"cache"` block keyed by playlist ID
(`_save_tracks_cache`/`_load_tracks_cache`), written by `_run_check_against_downloads()` itself so there
is no separate save call in `fetch()` to forget. `restore_cached_tracks()` reloads it on tab build,
refusing to clobber live tracks or Simulate mode. Cache entries are pruned to the playlists still in
history, so the file cannot grow without bound, and a corrupt or absent state file degrades to "nothing
restored" rather than breaking the build. Simulate-mode data is never persisted (guarded on
`_state["simulated"]`, same shape as the existing `_poll_should_skip` guard), so a reload can never
resurrect synthetic tracks as if they were a real playlist. Clear forgets the cached playlist
(`_forget_cached_playlist`) but deliberately keeps the history: Clear resets the view, it does not erase
where you have been. One JSON file, no new dependencies.

**`progress_bar()` segment maths extracted and tested.** The downloaded/missing/extra to segment-width
arithmetic was inline in the refreshable and only verifiable by eyeballing the UI. Now
`progress_metrics(downloaded, missing, extra)` returns `total`, `pct_complete` and the three `widths`,
with `_segment_shows_label()` holding the "is this segment wide enough for an inline label, or does it
fall back to a title tooltip" threshold. `progress_bar()` is a pure consumer of both. Tests pin the
behaviour that was previously implicit: widths always sum to 100, `pct_complete` counts the playlist only
(extras in NewMusic are not playlist tracks and must not move completion), and browse mode with extras and
no playlist is 0% rather than a division by zero.

**`clear()` fully resets, and is finally testable.** `clear()` and `_run_check_against_downloads()` were
closures nested in `build()`, so nothing about Clear could be tested without a live NiceGUI page. Both are
now module-level (`clear_tab_state()`, `_run_check_against_downloads()`), with the button handler reduced
to blanking the input and delegating; refreshes go through the existing `_refresh_hooks` indirection the
same way `simulate()` already did. Tests prove Clear resets sort column and direction, refreshes all four
panels including the history menu, drops the disk cache so a rebuild stays blank, keeps the history, and
leaves the NewMusic extras section intact.

**Test coverage gaps closed:** `_format_duration` (including a round-trip against `_length_to_seconds`),
the `_sorted_tracks` Length key (proving `"10:00"` sorts after `"2:00"`, which plain text does not),
Year blanks-last under reversal, and case-insensitive artist sort. The `_save_last_playlist`/`_load_history`
dedup-and-cap logic already had coverage from an earlier pass and was left as is.

## 2026-09-04 - Duplicate-resolution UI: the GUI can now express D/L/K for a library duplicate

Implemented the full item David unblocked from `[OPUS]` to `[SONNET]` on 2026-09-03: the GUI previously
had no way to see or change the exe's own D/L/K recommendation for a file that already exists in the
library - it silently auto-took the recommendation on every real run. Shipped in three layers, C# first
and fully tested before touching the GUI, per the original spec's ordering.

**C# side (`MusicIntegrator.cs`, `IntegrationManifest.cs`):** `LogEntry` gained a `LibraryDuplicate` block
(`DupLibraryPath`/`DupLibraryTrack`/`DupLibraryAlbum`/`DupNewAlbum`/`DupRecommendationKey`/
`DupRecommendation`/`DupReason`), populated in `PreScanFiles` from the existing `DupData` the duplicate
scan already computes - no new scanning logic, just surfacing data that already existed. `WriteJsonOutput`
was refactored to extract a pure, testable `BuildJson(List<LogEntry>)` (mirroring the existing
`TracksJson.Build`/`StatsJson.Build` pattern) so the new fields' JSON shape has direct unit coverage
without touching `Constants.LogsPath`. `IntegrationManifest` gained a `dupResolution` field per entry and
`GetDupResolution(filename, artist, title)`, using the same two-tier exact-filename-then-canonical-
rename-fallback lookup `Matches()` already uses, so a manifest resolution survives a TagFixer rename
between the dry run and the real run exactly like an accept/decline decision does.
`PresentDuplicateAndDecide`'s `noInput` branch now checks the manifest for an override before falling
back to auto-accept-recommendation - the pre-existing "no manifest -> take the recommendation" behavior
is completely unchanged when no override exists. New coverage:
`Code/Tests/MusicIntegratorDuplicateJsonTests.cs` (7 tests) plus 5 new tests in
`IntegrationManifestTests.cs`.

**Manifest/exe-invocation extension:** no new CLI flag - the existing `integrate --manifest <path>`
mechanism was extended in place, per the task's explicit instruction to reuse rather than invent a
mechanism. A manifest entry for a library-duplicate file now carries an optional `dupResolution` key
("D"/"L"/"K"); everything else about a real run (how `--manifest` is invoked, how declined files are
excluded) is untouched.

**GUI side (`gui/routing.py`, `gui/tabs/integration.py`, `gui/theme.py`):** `parse_routing_file` extracts
the new fields into every entry dict. `IntegrationState` gained `dup_resolutions` (filename -> explicit
D/L/K override) and a `dup_resolution(e)` method that returns the override if one was made, else the
exe's own `dupRecommendationKey` - the same lookup used both for "left untouched in the UI" and for what
the guarded `_write_manifest()` writes, so there is exactly one default, not two that could drift apart.
The review card shows a new "Library duplicate" badge (distinct from the existing, unchanged "In-batch
duplicate" badge - two unrelated concepts per the spec) and, only for a library-duplicate entry, a
CLI-parity info block (library copy's path/track/album, the new file's album, the exe's recommendation
and reason) plus a three-button D/L/K resolution control defaulting to the recommendation. The review
stage's filter chips gained a fourth option, "Library duplicates", alongside the existing All/New
folders/Duplicates-conflicts. Simulate mode's sample data now includes a dedicated library-duplicate
entry (Kendrick Lamar - HUMBLE., a deluxe-album L-recommendation case matching the C# test fixture) kept
separate from the existing in-batch-duplicate sample so the two badges/filters can be exercised and
verified independently.

**Guarded boundary (`_write_manifest`, reviewed with extra care per task instructions):** the function
still writes exactly the `targets` list passed in (still `S.accepted` at the call site, unchanged) - the
only change is that a library-duplicate entry's own object gains one extra `dupResolution` key sourced
from `S.dup_resolution(e)`. Verified explicitly: `IntegrationState.declined` files are still excluded by
construction (never passed as `targets`), `decisions.get(fn, True)` is still a strict accepted/declined
partition with no undecided third state, and a non-duplicate entry's manifest shape is byte-for-byte
unchanged (no `dupResolution` key added). Two new tests
(`test_write_manifest_includes_dup_resolution_for_library_duplicates_only`,
`test_write_manifest_uses_explicit_dup_resolution_override`) lock this in.

Full `scripts\dev\verify.bat --no-pause` run: 270 C# tests + 131 GUI tests, all passing.

## 2026-09-04 - Sidebar nav grouped into Library Intake/Insight, "Library Browser" tab renamed to "Library"

Implemented the two backlog items from "Sidebar nav grouping + tab naming review, 2026-09-04": the flat
7-tab left nav (`gui/main.py`) is now split into two labeled sections matching the GUI's existing
mutate-vs-observe write-safety boundary - **Library Intake** (Acquire, Integration, Tag Fix) and
**Library Insight** (Statistics, Library, Mirror, Services). `TABS` was reordered to the new grouped order
and a new `NAV_GROUPS` list (`[(section_label, [tab_key, ...]), ...]`) drives rendering: the nav-building
loop now emits a `.nav-group-header` (`gui/theme.py`) before each group's buttons instead of one flat
`for key, label, badge in TABS` loop. Tab `key` values, `id="nav-{key}"`, the `tab-link`/`active` class
toggle, and the `?tab=` deep-link/`history.replaceState` JS are all untouched - grouping is purely a
render-order/visual change, verified live against the running dev server (deep links to `stats`,
`acquire`, `integration`, `tagfix`, `library`, `mirror` all land on the correct active tab with both
group headers rendered). Verb-only tab naming (Statistics -> Analyse, etc.) and nesting Tag Fix under
Integration were both explicitly considered and rejected per the backlog item - see the "Design Vision"
section of `docs/References/GUI-Architecture.md` for the rationale now recorded there.

Second, smaller item: "Library Browser" renamed to "Library" everywhere it's user-visible - the nav
label, the tab's own `<h1>` (`gui/tabs/library.py`), `gui/README.md`'s Tabs table and architecture
diagram, `docs/References/GUI-Architecture.md`'s CLI-parity table and build-status bullet, and a stray
mention in the Services tab's Last.fm stub text (`gui/tabs/services.py`). Tab `key` (`"library"`) and all
Python module/function names are unchanged. `python -m pytest gui/tests -q` stayed green throughout (120
passed).

## 2026-09-04 - Fix: one bad file no longer aborts the whole Integration batch

David's decision after the Simulate-mode review below flagged the design question: "errored items
should never be accepted, but one bad file should not break the entire batch." Two-part fix, GUI then C#:

**GUI side (`gui/tabs/integration.py`, `gui/tests/test_integration.py`):** `IntegrationState.accepted`
now unconditionally excludes `status=="error"` review entries regardless of the `decisions` dict. The
review card disables Accept and shows "Declined (error)" for them, `_bulk()`'s Accept All skips them, and
`_decide()` refuses to flip one to accepted even if called directly. Since an error entry can now never
reach `S.accepted`, it can never be written into the manifest or into Simulate mode's execute loop -
`run_execute_simulated`'s now-unreachable mid-batch-abort branch was removed. Live-verified: Simulate mode
went from "6 accepted, 0 declined" to "5 accepted, 0 declined", and Integrate All completed all 5 real
tracks with no abort (the track previously stranded `NOT RUN` behind the bad file now completes as
`DONE`). Full GUI suite green.

**C# side (`project/AudioManager/Code/Doer/MusicIntegrator.cs`):** the GUI fix only stops a *known-bad*
review-stage entry from reaching execution. The real engine's `RouteAllFiles()` had a separate, deeper
gap: an *accepted* file that fails unexpectedly at move time (unreadable file, or any other exception
during the file-system move) printed "INTEGRATION FAILED" and `throw`n, halting every remaining file in
the batch - confirmed via `Program.cs`'s constructor call site having no catch around it, so the exception
crashed the whole `integrate` run. Both throw sites (`!sf.IsReadable`, and the general `catch (Exception
ex)` around the move) now do what the file's sibling "missing required tag" branch already did: mark the
entry `Status = "error"` with a detail message, increment `skippedCount`, and `continue` - the rest of the
batch keeps going. No prior test coverage existed for this path (`ManifestRunner.cs` only tests routing
*decisions*, never real file-move/exception behavior), so `ScannedFile`, `LogEntry`, `DupData`, and
`RouteAllFiles` itself were changed from `private` to `internal` (the same seam already used for
`GetDestDir` in `RoutingTests.cs`) and a new `MusicIntegratorRouteAllFilesTests.cs` added: an unreadable
file no longer throws and the good file around it still routes; a real `Directory.CreateDirectory`
exception (forced by pre-creating a file at the destination directory's path) is caught and skipped, not
thrown; and the exact reported bug shape - a good file positioned *after* a bad one - reaches `moved`
rather than being stranded. All three pass; full C# suite green. No refactor of the
Constants-tied real-path coupling - deliberately out of scope, David's "keep testing safe against real
library" constraint met by using the pre-existing test-only `MusicIntegrator(testLibraryPath)` constructor
and temp directories throughout, never `Constants.AudioFolderPath`.

## 2026-09-04 - Fix: Acquire tab's 2s poll timer clobbered Simulate mode's sample data

`_poll_downloads()`'s `ui.timer(2.0, ...)` re-ran `_run_check_against_downloads()` unconditionally every 2
seconds while the Acquire tab was open, regardless of `_state["simulated"]` - so clicking "Simulate (sample
data)" worked for well under 2 seconds before the real `NEWMUSIC_DIR`/playlist match silently overwrote the
sample `downloaded`/`extra` data with real disk-scan results. Fixed by adding an early return in
`_poll_downloads()` - guarded via a new module-level `_poll_should_skip()` helper (returns
`_state["simulated"]`) so the guard condition is directly unit-testable without a live NiceGUI page, mirroring
`integration.py`'s existing `if not S.simulated:` pattern around its own background/real-execution logic.
Live-verified against the running dev server: clicked Simulate, watched the sample counts (2 downloaded, 4
missing, 3 extra, 33%) hold steady across 13+ seconds (multiple poll cycles).

## 2026-09-04 - Acquire tab: Simulate (sample data) mode

Added a "Simulate (sample data)" button to the Acquire tab, mirroring the Integration tab's existing
Simulate mode, so the tab can be exercised end to end (downloaded, missing and extra rows all present)
without a real Spotify account or a real NewMusic folder on disk - a testing/exploration harness, not a
user-facing feature. See "Acquire tab design" in `docs/References/GUI-Architecture.md` for the detail.

## 2026-09-04 - Fix batch: Acquire tab live-review polish (input width, redundant header, history dropdown, percentage)

David's live review of the Acquire tab right after the incident-batch fixes above surfaced four small
polish items, all in `gui/tabs/acquire.py`, fixed in one commit:
- Playlist URL/ID input widened from `360px` to `520px` so a full Spotify playlist URL no longer
  truncates mid-string.
- The browse-mode "NEWMUSIC FOLDER (N)" section-header row above the file list was dropped when no
  playlist is loaded, since the top progress bar already shows the same count via
  `_extra_segment_label()`'s browse wording. Diff mode's "IN NEWMUSIC, NOT IN THIS PLAYLIST (N)"
  header is untouched - it still labels a real second section there.
- The playlist history dropdown no longer lists the currently-loaded playlist as a re-selectable
  option (filtered out by id against `_load_last_playlist_id()`), and gained an "OTHER PLAYLISTS"
  header above the remaining entries, shown only when at least one alternative exists.
- The downloaded/missing progress bar gained a "N%" figure in the top-right corner, computed as
  downloaded over downloaded-plus-missing (guarded against a zero-total division separately from the
  bar's existing empty-state early return).

## 2026-09-04 - Fix batch: Acquire tab incident (Spotify fetch RuntimeError, hide_downloaded, blank extra-row tags, misleading framing, test blind spot)

A screenshot regression review on 2026-09-04 found the Acquire tab still broken: every Spotify fetch
raised a `RuntimeError`, the "Hide downloaded" checkbox had no visible effect, and "N extra in
NewMusic" read as an anomaly even with no playlist loaded. Root cause of the fetch failure was in the
sibling SpotifyTools repo: commit `624d88c` re-nested `spotify_tools/` one directory deeper
(`spotify_tools/` -> `src/spotify_tools/`) but left `CONFIG_PATH`'s `BASE_DIR` at two `dirname()`
calls, so it resolved to a nonexistent `src/config/config.json` instead of the real
`config/config.json` at the repo root. Every test in `test_config.py` passed an explicit `path=`
override, so the bug shipped through a green 214/214 suite - fixed in SpotifyTools commit `2fca625`
(add a third `dirname()`, plus a regression test that imports the bare `CONFIG_PATH` constant with no
override and asserts it exists).

Four AudioManager-side fixes followed, one commit each with `verify.bat` green in between:
- `d5542c78` - `hide_downloaded`'s filter only applied to the fetched-tracks loop, not the "extra"
  (NewMusic-only) rows loop; added the same skip there, since every extra row is downloaded by
  definition.
- `1809faf3` - extra rows rendered blank Album/Year/Length because the pipeline only ever carried
  `(artist, title, url)`, never the file's own `Path`. Threaded `Path` through
  `_scan_newmusic_filenames` -> `find_extra_newmusic_files` -> `_state["extra"]` and added
  `_read_mp3_tags()` (mirroring `gui/art.py`'s read-only mutagen precedent) to populate those columns
  from real ID3 tags at render time.
- `948b7f2c` - "N extra in NewMusic" read as a warning even with no playlist ever loaded. Added
  `_state["playlist_loaded"]` and extracted `_extra_segment_label()`/`_extra_batch_header()` so the
  wording switches to plain browse framing ("N files in NewMusic") until a playlist is fetched, then
  reverts to diff framing once one is loaded. Underlying reconciliation untouched - label-only fix.
- `9850f6c5` - closed the test blind spot that let the Spotify regression ship silently:
  `_spotify_client()` was never invoked or imported by `test_acquire.py`, so a green AudioManager
  suite proved nothing about the fetch path. Added tests for both the config-not-found (raises
  `RuntimeError`) and config-present (constructs client, no network call) branches.

`56c8a316` closed out the batch by rewriting the "Acquire tab design" section of
`docs/References/GUI-Architecture.md` to match the shipped behavior: the shared reconciliation model,
the browse-vs-diff framing as the tab's actual conceptual model rather than a bugfix note, extra-row
ID3 reads, `hide_downloaded` covering both row kinds, and the `_spotify_client()`/`CONFIG_PATH`
dependency.

## 2026-09-03 - Investigation: real embedded album art extraction confirmed against NewMusic (read-only)

David asked where cover-art images come from and whether they can be read
straight from real NewMusic files without running real integration. Answer:
`gui/art.py`'s `get_thumbnail()` reads the embedded ID3 APIC/cover frame
directly out of each MP3 via `mutagen` (`ID3(file_path).getall("APIC")`,
falling back to `mutagen.File(...).pictures`), downscales it with Pillow to a
300px JPEG, and disk-caches it under `gui/.cache/thumbs/` keyed by an MD5 hash
of the track id. It never touches the review pipeline's synthetic placeholder
art (only Simulate mode uses that) and never writes into the library or
NewMusic inbox - reading is strictly read-only against the source MP3.

Validated this against the real NewMusic inbox with a standalone read-only
script (`_extract_image_bytes` + `_to_jpeg_thumb` called directly, bypassing
`get_thumbnail()`'s cache-write step so nothing touched
`gui/.cache/thumbs/`, and never calling the Integration tab's exe subprocess):
all 25 NewMusic MP3s had extractable embedded art, total extraction time
0.42s. Conclusion: real thumbnails are technically ready to wire into the
Integration tab's review cards whenever that's wanted - the extraction path
already works reliably and fast against real files, this just hasn't been
connected to that UI yet.

## 2026-09-03 - Fix: album art tile hidden by a browser-extension CSS collision on `.cover-art.lg`

The IDEAS.md item claimed `.cover-art.md` review-card tiles measured `0x0` via
`getBoundingClientRect()`. Live MCP measurement in Simulate mode disproved that: all 6 tiles came
back a correct 56x56px in both computed style and bounding rect - that branch was never actually
broken. The real bug was one tab over and one size class over: Library Browser's Grid view, the
`.cover-art.lg` real-`<img>` branch (only reachable with a live thumbnail cache, so Simulate mode
never exercised it). Live measurement there showed `getComputedStyle(el).display === "none"` with
`getBoundingClientRect()` returning `0x0` - but no rule in any of the 6 accessible
`document.styleSheets` set `display` on `.cover-art.lg`, and forcing `display:flex !important` via
inline style immediately fixed it (tiles rendered at the correct size, images decoded fine,
`naturalWidth`/`naturalHeight` were already 300x300). Conclusion: something outside the app's own
CSS - not visible to page JS, almost certainly a browser extension's (or Brave Shields') cosmetic
element-hiding filter matching the bare, generic `.lg` class - was stomping it. Renamed the class
to `.cover-lg` in `gui/theme.py` and `gui/tabs/library.py` to remove the collision surface; confirmed
fixed live via MCP screenshot with no JS override needed after the rename and hot-reload.

## 2026-09-03 - Fix: Simulate mode no longer opens NewMusic thumbnails it claims not to touch

T3 polish item. `review_card()` unconditionally called `routing.newmusic_path()` then
`get_thumbnail()` for every entry regardless of `S.simulated` - harmless (nothing modified, sample
filenames don't exist) but contradicted the Simulate banner's "no NewMusic file is read" guarantee.
Gated the lookup on `not S.simulated`. Full suite: 255 C# passed, 102 GUI passed (`verify.bat`).

## 2026-09-03 - Feature: surface the post-run CONFIDENCE REPORT instead of hiding it in debug

`PrintConfidenceReport` (`MusicIntegrator.cs` ~900-990) is the exe's strongest guarantee that a
claimed-successful real run actually succeeded: a `Files in NewMusic: N | Moved: M | Skipped: S`
count check (flagged `[ERROR] Count mismatch!` on disagreement) and a destination sanity check that
re-reads every moved file with TagLib. Both printed to `S.exec_lines` but were only reachable via
"Advanced / raw output (debug)" - calling the strongest integrity signal in the whole run "debug"
buried it exactly where it mattered most. Added `routing.parse_confidence_report(lines)` and a
`confidence_report_strip()` pass/fail banner on the execute-result stage (reusing the
`.libchecker-strip` clean/dirty styling from the Projected LibChecker feature above), wired into
both the real run's `_finish_execute()` path and Simulate mode's synthetic execute path so the
strip is exercised without touching a real file. 4 new routing-parser tests + full suite: 255 C#
passed, 102 GUI passed (`verify.bat`). Not yet verified in a live browser - the Chrome MCP
extension was not connected in this session (`tabs_context_mcp` failed 3x with "extension not
connected"); visual/interactive confirmation is still outstanding.

## 2026-09-03 - Feature: surface the dry run's Projected LibChecker verdict on the review stage

Fourth item from the Opus design-review pass. `integrate --dry-run --json-output` already computes
and prints the "Projected LibChecker (Dry Run)" block every scan (`MusicIntegrator.cs`
`RunProjectedLibChecker`, ~1939-2034) - the actual answer to "will my library still be clean after
this run" - but it only ever landed in `S.scan_lines`, reachable by expanding the collapsed
"Advanced / raw output (debug)" panel, while the confirm dialog claimed the run was "protected by
the exe's own pre-integration safety gate" without showing any evidence of that gate's result.
Added `routing.parse_projected_libchecker(lines)` - a pure parser matching the header, the
" - Projected library: N current, -R removals, +A additions = P projected" summary line, the
" - LibChecker: Clean" line when clean, and summing every " - Total hits: N" subtotal when dirty
(plus a distinct `skipped` case for the "SKIP: could not load current library tags" early-return
path) - and render it as a `.libchecker-strip` pass/fail/skip banner at the top of the review stage.
Simulate mode gets a synthetic clean verdict so the strip renders without a real exe call.

4 new tests in `gui/tests/test_routing.py` (absent/clean/dirty-with-summed-hits/skipped).
`scripts\dev\verify.bat --no-pause` green (255 C# + 97 GUI).

## 2026-09-03 - Fix: bulk accept/decline ignored the active review filter, chip count mismatched it

Third fix from the Opus design-review pass. `_bulk` (backing "Accept all"/"Decline all") looped
`S.entries` unconditionally instead of `S.filtered()` - filtering the review stage to "New folders"
or "Duplicates / conflicts" then clicking "Decline all" silently declined the whole batch, not just
the visible rows, a genuinely destructive mis-click on a large real batch with no undo beyond
re-accepting by hand. Fixed by having `_bulk` iterate `S.filtered()`. Separately, the "Duplicates /
conflicts (N)" chip count used `inBatchDuplicate` alone while `filtered()`'s `conflicts` branch also
matches any non-clean `status`, so the two disagreed; aligned the chip's count expression with
`filtered()`'s actual condition.

2 new tests in `gui/tests/test_integration.py`: bulk-with-active-filter only touches filtered rows,
bulk-with-"all"-filter touches everything. `scripts\dev\verify.bat --no-pause` green (255 C# + 93 GUI).

## 2026-09-03 - Fix: failed-run UI - error modal destroyed, false-green progress bar, duplicated filename

Second fix from the Opus design-review pass, working down the ranked backlog. Three compounding
bugs in the failed-run path: (1) `_finish_execute` called `show_error_modal(...)` then
`S.refresh()` on the next line - the refresh rebuilds the `@ui.refreshable` slot the dialog was
created inside, destroying it before it can ever be seen (confirmed via DOM inspection during the
review: zero `.q-dialog` elements survive a real failure). Reordered so refresh runs first and the
modal opens after. (2) The progress bar rendered the success blue-to-green gradient at 100%
regardless of outcome - added `IntegrationState.exec_ok` (set by `_finish_execute`, reset on every
new scan/simulate/execute) and a `.progress-track .fill.fail` red override in `gui/theme.py`
(needed twice - once for the base gradient, once to beat the later animated-shine rule that shares
its specificity and comes after it in the stylesheet). (3) The failure summary duplicated the
failed filename with no separator (`"...Error processing file: X 1 file failed: X."`) - the file
is already named by `result.interpreted()`'s cause line, so the redundant clause was dropped
outright rather than just adding a separator. Also gave `st-notrun` its own colour (amber/accent3)
- it shared `st-queued`'s exact grey before, making "queued" and "not run" indistinguishable.

4 new tests in `gui/tests/test_integration.py`: modal-opens-after-refresh ordering (via a
call-order list), `exec_ok` set true/false, and a regression assertion that the failed filename
appears exactly once in the summary. `scripts\dev\verify.bat --no-pause` green (255 C# + 91 GUI).

## 2026-09-03 - Fix: `_update_exec_status` mislabelled successful files as "not run" after a mid-batch failure

Top-priority fix from the Opus design-review pass below. Root cause: `_update_exec_status` looked
for `[moved]`/`moved:`/`[skipped]`/`[error]`/`[failed]` substrings, but the real exe's per-track
output is `[AUTO] {Artist} - {Title}` (success), `[SKIP] {Artist} - {Title}: ...` (deliberately
skipped), and, only on the halt-on-error path, `Error processing file: {filename}` - none of the
old patterns ever matched, so every row stayed `queued` until end-of-run, and `_finish_execute`'s
failure branch then relabelled every still-`queued` row `NOT RUN`, including files the exe's own
output already confirmed as moved. Rewrote `_update_exec_status` to match on "artist - title" tag
text for `[AUTO]`/`[SKIP]` lines (both now settle straight to `done` - there's no observable
"moving" state between them, the exe never prints a start-of-file line) and on filename for the
`Error processing file:` line, with the same longest-match anti-cross-contamination guard as
before. `_finish_execute` and the failure-labelling logic (`_failed_filename_from_output`) needed
no changes - they already only had to trust `S.exec_status`'s per-file state, which is now correct.

Updated 4 existing `_update_exec_status` unit tests to the exe's real output formats and added an
explicit regression assertion in the simulated-partial-failure test: every sample entry processed
before the injected failure must land on `done`, never `notrun`. `scripts\dev\verify.bat --no-pause`
green (255 C# + 88 GUI tests).

## 2026-09-03 - Integration tab: Opus design review via Simulate mode

An Opus subagent exercised the new Simulate mode end to end via browser automation (Simulate path
only, verified the confirm dialog before every click - no real integration was ever run) to review
the Integration tab's design/aesthetics/UX against project history and the old CLI's interactive
menus. Findings logged to `docs/Development/IDEAS.md`: the existing `_update_exec_status`
false-match item was escalated to TIER 1 after the review proved it now causes a real correctness
bug (successfully-moved files get mislabelled "not run" after a mid-batch failure, not merely
"decorative" progress), plus ten new items - a broken failed-run UI (no error modal, false-green
progress bar, string glitches), the missing duplicate-resolution UI (CLI's D/L/K/Q menu has no GUI
equivalent at all), the hidden Projected LibChecker verdict and post-run count/sanity
reconciliation (both computed, both buried behind a panel labelled "debug"), missing scan-ahead
batch context (route distribution, Misc auto-migration notice, compilation-album detection), and a
review-card layout/hierarchy critique (wasted width, inverted typography hierarchy, noisy "clean
route" badges, weak decline affordance, no keyboard path) plus smaller GUI bugs (art tile never
renders, bulk accept/decline ignoring the active filter, simulate mode's own thumbnail-lookup
exception to its no-NewMusic-access claim, dialog/stepper polish). No fixes implemented - reported
to David for prioritization per this workspace's backlog-first rule.

## 2026-09-03 - Integration tab: Simulate mode (UI dry run, no exe/NewMusic access)

Built ahead of an Opus design-review pass so the review could exercise every Integration-tab
stage and screenshot the review-card design without ever risking a real integration run.

`gui/tabs/integration.py`: a new "Simulate (sample data)" button on the scan stage
(`run_simulate()`) loads `_sample_entries()` - six synthetic entries covering every review-card
state (clean route, new-folder scan-ahead, in-batch duplicate, tag-change chips, an
error/unresolved status, one more clean route) - straight into stage 2, with no exe call at all.
Confirming on the confirm stage runs `run_execute_simulated()` instead of the real `run_execute()`
when `IntegrationState.simulated` is set: it streams synthetic lines through the exact same
`_update_exec_status`/on_line path a real run uses, using the exe's real confirmed output formats
(`[AUTO] {Artists} - {Title}`, halting on the sample "error" entry with
`Error processing file: {filename}`) - so it faithfully reproduces real behavior including the
already-logged "decorative progress" bug (`_update_exec_status` doesn't match `[AUTO]` lines).
`run_execute`'s post-run finalization was extracted into a shared `_finish_execute(result)` so
both the real and simulated paths are interpreted identically. A yellow SIMULATED banner
(`gui/theme.py` `.simulate-banner`) stays visible across all four stages, and the confirm dialog's
wording changes when simulated. `S.simulated` is reset to `False` on a real scan and on "New scan".

5 new tests in `gui/tests/test_integration.py` (sample-data shape, never touches the real runner,
`run_scan` clears a leftover simulated flag, the simulated run reaches `done`, and the simulated
partial-failure path relabels exactly like a real one). `scripts\dev\verify.bat --no-pause` green
(255 C# + 87 GUI tests).

---

## 2026-09-03 - Partial-failure runs now labelled "not run" instead of "failed"

Closed the last `[OPUS]`-tagged pre-batch item. Opus subagent judged the exe's real output
(`MusicIntegrator.cs`: sequential per-file loop, halts on first error, prints `Error processing
file: <filename>` before stopping) supports one reliable distinction and no more: the named file
in that line was genuinely attempted-and-failed; everything still `queued`/`moving` after it was
never reached. Per-track success/skip lines (`[AUTO]`/`[SKIP] {Artists} - {Title}`) carry tag text,
not filenames, so a full attempted-vs-not-attempted mapping for every file isn't supported by
current output - verdict was the cheap relabel plus this one extra, not the full fix.

`gui/tabs/integration.py`: added `_failed_filename_from_output()` (parses the halt line);
`run_execute`'s non-ok branch now marks the named file `failed` and everything else still
queued/moving `notrun`, appends a "not attempted" count and the failed filename to `exec_summary`;
added `EXEC_STATUS_LABELS` so the row text ("not run") is decoupled from the CSS class (`st-notrun`,
`gui/theme.py`, widened `.progress-row .st` from 64px to 84px so the label doesn't clip). 4 new
tests in `gui/tests/test_integration.py` (extraction, named-failure + notrun split, cancel path).

Found and logged separately (not fixed here): `_update_exec_status` matches `[MOVED]`/`[SKIPPED]`
strings the exe never actually prints, so live per-track progress rows stay `queued` until the
end-of-run bulk flip regardless of this fix - see IDEAS.md TIER 2.

`scripts\dev\verify.bat --no-pause` green after (255 C# + 82 GUI tests).

---

## 2026-09-03 - Integration-tab test coverage: routing.py and the manifest call site

TIER 2 items, integration-focused. No production code changed - both were pure coverage gaps.

**`routing.py` had zero test coverage.** Added `gui/tests/test_routing.py` (13 tests): `parse_routing_file`'s
defaults-on-missing-field contract, dropping non-dict/no-filename entries, non-string optional fields
(e.g. a JSON `null` where a string is expected) defaulting safely instead of propagating the wrong type,
UTF-8 BOM handling; `routing_path_from_output`'s regex extraction, its fallback to the newest
`routing-*.json` in `LOGS_DIR` when the matched path doesn't actually exist on disk, and the
no-match-found `None` case; `newmusic_path`'s join behaviour including relative subfolders.

**`run_execute`'s manifest-writing call site had no test that declined tracks are excluded.**
`IntegrationState.accepted`/`declined` and `_write_manifest` were already tested individually, but
nothing exercised `run_execute` itself - the actual code path between a user's Decline click and the
manifest the exe receives. Added `test_run_execute_writes_manifest_excluding_declined_tracks` to
`gui/tests/test_integration.py`, mocking `runner.run` and asserting the written manifest contains only
the accepted filename. Passed on first run - confirms the call site was already correct, closes the
coverage gap.

`scripts\dev\verify.bat --no-pause` green after both (255 C# + 78 GUI tests).

---

## 2026-09-03 - Three pre-batch integration-tab defects fixed

Ranked-risk items from IDEAS.md TIER 2 "Do these before the next real batch integration", closed one
per commit, each with `scripts\dev\verify.bat --no-pause` green after.

**A file added to NewMusic after the dry run was moved without ever being reviewed.** `run_execute` in
`gui/tabs/integration.py` passed `--manifest` only when `S.declined` was non-empty, so an
accept-everything run (the common case) invoked a bare `integrate --no-input` and the exe re-scanned
`NEWMUSIC_DIR` from scratch - anything that arrived between the Stage 1 dry run and the Stage 4 execute
(e.g. via the Acquire tab downloading into the same folder) was integrated with no review. The manifest
write was extracted into `_write_manifest()` and is now always called and always passed. Test:
`test_write_manifest_passes_manifest_flag_with_zero_declines`.

**Substring filename matching cross-contaminated per-track status.** `_update_exec_status` matched a
target filename via `fn.lower() in low`, so an accepted filename that is a substring of another accepted
filename (`Song.mp3` inside `Another Song.mp3`) had its status flipped by the other file's log line. Now
a match only counts when no other target's filename containing it as a substring also matches the same
line, so the longer/more specific filename wins. The "everything before this one has finished moving"
cascade relied on the same substring signal and was removed with it. The regression-documenting test at
`gui/tests/test_integration.py` was inverted to assert the correct (non-contaminating) behaviour.

**The GUI's own record of a run was capped and ephemeral.** `S.exec_lines` is capped at 300 lines and
cleared at the start of every run, and the GUI wrote no log of its own. `run_execute` now opens
`gui/.cache/run-logs/integration-<timestamp>.log` at the start of each run and tees every exe output
line to it, in addition to the existing in-memory `S.exec_lines`.

Left open, on purpose: the partially-failed-run status labelling (same file, lines ~360-365) needs a
design decision about the exe's real output format and stays `[OPUS]` in IDEAS.md.

---

## 2026-09-02 - verify.bat now covers the Python GUI suite (the repo's objective judge)

`scripts\dev\verify.bat` previously built the C# and ran `AudioManager.exe --verify` and nothing
else, while `gui/tests/` had six test files and no wired entry point anywhere under `scripts/`. The
repo's single documented pass/fail command therefore could not see a regression in any Python GUI
code, which is where all current backlog work lives. It now runs `python -m pytest gui\tests\ -q`
from the repo root after the C# verify, captures both exit codes separately, fails if either fails,
and prints the verdict as the last line of the log: `[PASS] C# verify + GUI tests` or
`[FAIL] C# verify=<code> GUI tests=<code>`.

Verified by running it: 255 C# plus 62 GUI tests, exit 0. The failure path was proven with a
deliberately failing temporary test, which returned exit 1 and the FAIL line with pytest's own
output visible, and was then removed.

This is what moved the Sonnet-readiness verdict in IDEAS.md from "bounded delegated tasks only" to
"unattended sessions, except the guarded decline-to-exe-arguments path". A delegated session can now
establish for itself whether what it wrote is correct.

## 2026-09-02 - [GUI] Integration tab test coverage and `_esc` hardening (backlog items closed)

Two IDEAS.md TIER 2 entries added by the same day's deep-dive were already resolved by commits
`ba94271c` (new integration/acquire tests) and `64ce7397` (`_esc`/`clear()` maintainability fixes)
but had not been removed from the backlog. Verified against the current source and closed here:
`gui/tests/test_integration.py` exists and covers `IntegrationState.accepted`/`declined`, `_bulk`,
`_decide`, `_esc` and `_update_exec_status`, including a deliberate regression-documenting test for
the substring-filename cross-contamination case; and `_esc` is no longer hand-rolled, it delegates
to `html.escape(text, quote=True)` (`gui/tabs/integration.py` line 441-442) with a test asserting
quote escaping. The substring-matching bug itself stays open and has been promoted into the new
batch-integration risk sub-section of IDEAS.md, since the test documents it rather than fixes it.

A stale backlog entry is worse than a missing one: a delegated session picking up "`_esc()` is a
hand-rolled escaper missing quote-escaping" as written would have re-broken correct code.

## 2026-09-02 - [GUI] Acquire tab deep-dive fixes: history-picker no-op, color drift

Deep-dive audit of Acquire/Integration tabs found `_apply_history_pick` (`gui/tabs/acquire.py`)
called `fetch()` - an `async def` - directly with no `await`/`asyncio.create_task(...)`, so
clicking a playlist in the history menu set the input text but silently discarded the fetch
coroutine and never loaded tracks. Fixed by wrapping in `asyncio.create_task(fetch())`, matching
`integration.py`'s established pattern. Also fixed downloaded-row highlight color drift: the
hardcoded `rgba(64,150,255,0.08)` didn't match `--accent` (`#5b8cff` = `rgb(91,140,255)`) and
wouldn't retint with `apply_mood()`; corrected to `rgba(91,140,255,0.08)`.

## 2026-08-31 - [GUI] Acquire tab MVP - Stage 2 (Acquiring) automation

Replaces the terminal `open_playlist.py` Enter/'q' loop for the mechanical half of Stage 2
(judgment calls - which tracks to like, which Deemix search result to pick - stay manual by
design, see the `/think` writeup in the introducing commit). Backend: `SpotifyInterface` gained
`get_liked_track_uris`/`remove_liked_tracks`/`get_or_create_playlist`, implemented in both
`RealSpotifyClient` (new `user-library-read`/`user-library-modify` scopes - one-time browser
re-consent) and `SimulatedSpotifyClient`, plus `src/acquire.py`'s `move_liked_to_playlist()`
orchestration (SpotifyTools repo, named SpotifyPlaylistGen at the time). GUI: new Acquire tab (slots in right after Statistics)
- sync Liked Songs to an "AudioManager Inbox" playlist, fetch-and-open Deemix links per track
(reuses `open_playlist.py`'s extracted `_build_deemix_url`), and a read-only fuzzy-match Verify
Downloads scan of `NEWMUSIC_DIR` against the fetched track list. Deliberately cheap/MVP - see
TIER 2/3 for the polish items deferred out of this build. Design: `GUI-Architecture.md` "Acquire
tab design".

---

## 2026-07-03 - Fable session round 2: five [GUI] items closed (visual + functional)

Second pass of the Fable promo session, iterated live in Chrome against the running GUI:

- **Statistics header mismatch (TIER 1):** root cause was NiceGUI `ui.html` wrappers shrink-wrapping
  inside flex columns - the stat-tile row rendered as a narrow vertical column. Fixed with explicit
  full-width wrappers; tiles now match the mockup's responsive row.
- **Fluid dynamic UI (TIER 1):** motion layer across all tabs - pointer-tracking spotlight on
  panels/tiles/cards (delegated JS + CSS vars), hover lift + border warm + shadow bloom, nav
  slide/glow, staggered tile entrance, tab-switch transitions, pulsing freshness dot, animated
  progress shine, cover-art zoom, background depth wash, slim scrollbars, reduced-motion respect.
  Chart layer: entrance animation, donut center headline + rounded segments + hover glow.
- **`integrate --manifest <accepted.json> --no-input` (TIER 1):** selective per-track integration.
  New `IntegrationManifest` (minimal JSON parse, no new deps) matches by raw dry-run filename OR
  the canonical TagFixer rename, so manifests survive renames between dry run and real run. Filter
  applies before scan-ahead + duplicate review (declined files influence nothing). Bad/empty
  manifest exits 1. 9 new tests; verify.bat 255/255. GUI writes gui/.cache/accepted-manifest.json
  and executes selectively when tracks are declined - declines no longer block execution.
- **Commit AudioMirror action (TIER 1):** Mirror tab one-click commit (confirm dialog, editable
  message, git add -A + commit, local only). The last terminal step in the integration loop is gone.
- **Mood-reactive theme (TIER 2):** the dominant genre in analysis-stats.json tints the accent
  system (accent + brand gradient + spotlight + chart palette) with a nav "Mood" chip. Hip Hop
  library reads electric violet-blue; rock would read hot red, jazz warm amber, etc.

Also: `AM_GUI_NO_BROWSER=1` env flag (dev restarts stop spawning browser tabs), library grid card
meta switched to genre/year (force-regen resets mirror mtimes, making addedDate read as noise).

---

## 2026-07-01 - MusicIntegrator constructor decomposition

Broke the 400+ line public constructor into three per-phase private methods: `RunDuplicateReview` (Step 2 - presents in-batch duplicates, returns false on user quit), `ExecuteDuplicateDecisions` (Step 3a - executes D/L decisions, groups their output before routing begins), and `RouteAllFiles` (Step 3b - the main per-file routing loop). The constructor is now a ~95-line orchestrator that calls each phase in order and stops early via the returned bool where the original had inline `return` statements.

Pure extract-method refactor - no behavior change. No existing test exercises the constructor's orchestration directly (only `GetDestDir` routing logic is tested in isolation), so verification relied on build success, the full 230-test suite passing unchanged, and careful line-for-line preservation reviewed via diff. NewMusic was empty during this session, so a live dry-run diff of the routing loop itself wasn't possible - worth a spot-check next real integration run.

## 2026-07-01 - TagFixer full-pipeline test (real MP3 file I/O)

Closed the last "expansion candidate" in the automated-tests item: prior TagFixer tests only exercised pure functions (`RemoveParentheticals`, `StripAlbumSuffixes`, `ExtractAndFixArtists`) - none touched the actual TagLib# read/write/rename path.

Extracted the per-file loop body from `TagFixer`'s constructor into a testable `ProcessFile(sourcePath)` instance method, and added an internal `ForTesting(bool dryRun)` entry point that skips the real `Constants.NewMusicPath` directory scan (constructor behavior for production use is unchanged). Tests synthesize a minimal silent MPEG1 Layer III frame at run time - no binary fixture committed - so TagLib# can open, tag, save, and reload a real file. Four new tests cover: tag cleanup written to disk, filename rename to `Artist - Title.mp3` convention, idempotent skip on a second pass over already-clean output, and TCMP flag set on save. 230/230 tests pass.

Did not touch `TagFixer`'s public constructor signature - no folder-path parameter was added, per the constraint in CLAUDE.md that TagFixer must only ever operate on the NewMusic folder.

---

## 2026-06-30 - Dry-run output: L-decision destination + Misc migration WARN fix

Two causally linked dry-run output improvements:

**L-decision routing destination:** Dry-run now prints `[DRY RUN] Would route to: Artists\X\Singles\X - Title.mp3` after "Would keep new file" for [L]-decided duplicates. Previously the destination was invisible - the file was shown as "would keep" but you couldn't see where it would land without running real integration.

**Misc migration WARN fix:** When a Misc migration candidate was also [L]-deleted in the same real-integration run, `RunMiscMigration` would print `[WARN] Not found in library (stale mirror?)` because the file was already deleted. Now tracks L-deleted library paths in `_lDeletedLibraryPaths` and prints `[INFO] Already deleted by duplicate resolution: {file}` instead - eliminating the false alarm.

---

## 2026-06-30 - Dry-run projected LibChecker

Implemented dry-run projected LibChecker: before any files move, the integrator builds a projected post-integration tag list in memory (`current parsed tags` - removals from L-decisions and Misc migration sources + synthesized destination TrackTags with fixed tags) and runs LibChecker on it. Predicts the real library state with zero false positives (deletions correctly modelled). Makes "a clean integration leaves a clean library" enforceable before any irreversible move.

Core design: `BuildProjection(currentTags, additions, removals)` is a pure static function (testable in isolation). `RunProjectedLibChecker` handles I/O (loads current tags via Parser) and orchestration. Called in `dryRun` path between JSON output and `PrintRoutingResultsAndFinish`.

10 tests added in `ProjectedLibCheckerTests.cs` covering: empty library add, pass-through, removal, L-decision replace, Misc migration move, LibChecker pass/fail on projection, multi-file scenarios.

Also fixed a critical regression discovered during testing: `GetRelativePath` returned URI forward-slash paths, which caused `PruneOrphanedXmls` to delete all 5694 XMLs on every incremental run (mixed-slash expectedXmlPaths never matched Directory.GetFiles backslash output). Fixed by adding `.Replace('/', Path.DirectorySeparatorChar)`. Regression test added.

---

## 2026-06-30 - Reflector: prune orphaned XMLs in incremental pass

Incremental Reflector created XMLs for new MP3s and refreshed XMLs when the MP3 was newer - but never deleted XMLs for MP3s that had been removed from the library. Ghost XMLs accumulated and tripped LibChecker (duplicates, misplaced songs) on the next non-force-regen analysis run.

Fix: `CreateFiles()` now builds a `HashSet<string>` of expected XML paths as it processes each MP3. After the main pass, `PruneOrphanedXmls()` deletes any XMLs not in the set. Guard: pruning only fires in incremental mode (`!AgeChecker.RegenMirror`) and when `realFiles.Length > 0` (Audio root enumerated successfully - prevents accidental mass delete if the library is temporarily inaccessible). Force-regen already rebuilds from scratch so pruning is not needed there. Output line "XMLs pruned: N" shown only when N > 0.

Retires the "deleted MP3 persists until force regen" caveat. Completes the deeper fix for the 2026-06-28 ghost-XML incident (the immediate fix was adding ForceRegen before post-integration Reflector; this makes incremental runs equally clean). 4 new tests added.

---

## 2026-06-30 - AudioMirrorCommitter: fix stdout/stderr deadlock in RunGit

`RunGit` read stdout then stderr sequentially. When git writes enough to stderr to fill the 4KB pipe buffer (e.g. CRLF warnings for 5693 XML files during `git add AUDIO_MIRROR/`), the process hangs trying to write more stderr while the main thread is stuck in `stdout.ReadToEnd()`. Neither side can proceed - classic C# Process deadlock.

Fix: read stderr on a `Task.Run` background thread concurrently with reading stdout on the main thread. Both pipes drain simultaneously; no deadlock possible. Also changed `RunGit` from `private` to `internal` for testability, and added 3 new tests: `RunGit_CapturesStdoutFromStatusCommand`, `RunGit_CapturesStderrFromFailedCommand`, `RunGit_InvalidWorkingDirectory_ReturnsErrorMessage`.

The full "integration test that runs force-regen and verifies auto-commit completes within N seconds" remains aspirational - it would require a real AudioMirror clone and is lower value than the root-cause fix. Existing unit tests now cover the subprocess paths.

---

## 2026-06-30 - Album version-suffix normalization: fix split-album routing bug

`StripAlbumSuffixes` didn't match `(YYYY Remastered Edition)` - the remaster regex stopped at `Remaster(ed)` without the trailing `Edition` word. Result: `"Life After Death (2014 Remastered Edition)"` was left in the ID3 tag unchanged, scan-ahead stored the suffixed name as a dict key, `CountAlbumSongs` missed the library's existing `"Life After Death/"` folder, and `GetDestDir` built a NEW `"Life After Death (2014 Remastered Edition)/"` folder - splitting the album and tripping LibChecker.

Three changes:
1. `StripAlbumSuffixes` - extended remaster pattern to `(?:\d{4}\s+)?Remaster(?:ed)?(?:\s+Edition)?`; also added `(Mono)` and `(Stereo)`.
2. `FindAlbumFolder` (new helper) - fuzzy-matches existing library subfolders by stripping suffixes from folder names; handles the reverse scenario where the library folder HAS a suffix but the incoming tag is already clean.
3. `CountAlbumSongs` + `GetDestDir` - both now use `FindAlbumFolder` so count and destination path always agree on the same folder.

Result: incoming `"Life After Death (2014 Remastered Edition)"` album tag is stripped to `"Life After Death"` by TagFixer, counted against the `"Life After Death/"` library folder, and routed there. Also handles forward compatibility if the library folder was previously created with a suffix. 7 new tests added.

---

## 2026-06-28 - INVESTIGATE: multi-artist filename separator - no bug found

Investigated TIER 1 candidate: "multi-artist destination filename keeps `"; "` while artist tag normalizes to `";`"". Read on-disk MP3 filenames and AudioMirror XML `<Artists>` tags for two multi-artist files from the 2026-06-28 integration batch (`Akira The Don; David Lynch - KALI YUGA` and `Counting Crows; Vanessa Carlton - Big Yellow Taxi`). Both filenames and both `<Artists>` tags consistently use `"; "` (semicolon space). No mismatch. LibChecker correctly does not flag these files.

Latent risk identified and moved to TIER 3: TagFixer's `string.Join(";")` (no space) diverges from TagLib#'s `JoinedPerformers` (with `"; "` space), which could cause phantom `artistsChanged` detections for future files stored with Performers as a TagLib# array. No fix needed now - not manifested in real data.

---

## 2026-06-28 - Post-integration mirror integrity: force-regen + LastRunInfo.txt commit

Two bugs fixed together (causally linked - both affect post-integration reliability):

**Bug 1 - LastRunInfo.txt excluded from auto-commit:** `AudioMirrorCommitter.TryCommit()` staged only `AUDIO_MIRROR/` via `git add`, silently omitting `LastRunInfo.txt` at the AudioMirror repo root on every auto-commit. Fixed: `git add AUDIO_MIRROR/ LastRunInfo.txt`. CLAUDE.md rule: "AudioMirror audit metadata is as much part of the artifact as the data itself."

**Bug 2 - Post-integration used incremental Reflector:** After integration moves/deletes library files (`[L]` duplicate decisions, Misc migration), the post-integration Reflector ran incrementally - leaving orphaned XMLs for vacated locations. LibChecker then saw those ghosts and reported false issues. Confirmed 2026-06-28: 11 of 12 post-integration LibChecker hits were ghosts; only the B.I.G. album-suffix case was real. Fixed: added `AgeChecker.ForceRegen(path=null)` static method (sets `RegenMirror=true`, writes `LastRunInfo.txt`). Program.cs post-integration block now calls `AgeChecker.ForceRegen()` before `new Reflector(mirrorPath)`, ensuring a full mirror rebuild that prunes orphaned XMLs. Three new tests added: `ForceRegen_SetsRegenMirrorTrue`, `ForceRegen_WritesLastRunInfoFile`, `ForceRegen_UpdatesExistingLastRunInfoFile`.

---

## 2026-06-27 - LibChecker: detect missing artist semicolon delimiter in filenames

`CheckFilename()` now verifies that multi-artist tracks have the semicolon delimiter present in the filename. Previous check: each artist is a substring of the filename (passes for "T.I.Cee Lo Green" because "T.I." and "Cee Lo Green" are both substrings). New check: `string.Join(";", artists)` must also be a substring (fails "T.I.Cee Lo Green" but passes "T.I.;Cee Lo Green"). Two tests added: `LibChecker_MultiArtistFilenameMissingSemicolon_IsDirty` and `LibChecker_MultiArtistFilenameWithSemicolon_IsClean`. Now catches `T.I.Cee Lo Green - Hello.mp3` and `T.I.Dr. Dre - Popped Off.mp3` in the real library. Those files need manual rename via Mp3tag (see TIER 1).

---

## 2026-06-27 - Ye -> Kanye West: verified working in BULLY-DELUXE dry-run

Dry-run on the current NewMusic batch (2026-06-27) confirmed both overrides fire correctly:
- `artist-name-overrides.xml` `variant="Ye"` -> "Kanye West" normalizes all "Ye"-tagged incoming files during TagFixer
- `artist-aliases.xml` alias bridges duplicate detection: library has "Kanye West", batch files tagged "Ye" - duplicates correctly identified and auto-resolved
- BULLY vs BULLY-DELUXE duplicates auto-recommended [L] via `IsDeluxeVersionOf` detection
- All 7 BULLY-DELUXE songs route to `Artists/Kanye West/BULLY - DELUXE/` correctly

---

## 2026-06-27 - Ye -> Kanye West: config and tag normalization

`artist-name-overrides.xml`: added `<Artist canonical="Kanye West" variant="Ye" />` so TagFixer normalizes incoming "Ye" artist tags to "Kanye West" during integration. Existing library dry-run (84 files, 2026-06-27) showed no tracks with `Artist = "Ye"` - all already tagged "Kanye West".

`artist-aliases.xml`: reversed alias direction from `Kanye West -> Ye` to `Ye -> Kanye West` so duplicate detection correctly bridges both names when library has "Kanye West" and a batch file arrives tagged "Ye".


---

## 2026-06-27 - Duplicate preference: auto-prefer deluxe/expanded/remastered albums

When a duplicate is detected and the two copies come from different album versions (e.g., "BULLY" vs "BULLY - DELUXE"), `BuildDupData` now auto-recommends keeping the deluxe/expanded/remastered version via `IsDeluxeVersionOf`. Detection: the candidate album starts with the base album name as a prefix AND the candidate contains a deluxe keyword (DELUXE, EXPANDED, REMASTERED, ANNIVERSARY EDITION, etc.) that the base does not. If both are deluxe editions, no recommendation is made. Previously this showed "No version preference" and required user input on every such duplicate.

---

## 2026-06-27 - Routing reason wording: "only 0 songs" -> "no songs"

When albumCount is 0, routing reason strings produced "only 0 songs from album -> Singles/" (and the ATD People path produced "only 0 from this album"). Both now emit "no songs from album -> Singles/" for the zero case. Non-zero cases unchanged ("only 1 song", "only 2 songs").

---

## 2026-06-27 - RunScanAhead: normalize artist via TagFixer before storing batchAlbumCounts key

Some streaming providers embed Unicode artist variants (e.g. "JAŸ-Z" with Y-umlaut U+0178) in MP3 tags. `RunScanAhead` was storing these raw strings as keys in `_scanAheadBatchAlbumCounts`. `CountAlbumSongs` queries with the TagFixer-normalized name ("Jay-Z"). `StringComparer.OrdinalIgnoreCase` cannot bridge different Unicode code points (U+0178 vs U+0059), so the lookup returned 0 and all Blueprint songs routed to Singles instead of the album subfolder.

Fix: `RunScanAhead` now calls `TagFixer.ExtractAndFixArtists()` on each file's artist tag before using it as the `batchAlbumCounts` key (and also `batchCounts` for new-folder detection). This ensures the key matches what `CountAlbumSongs` will query: the canonical, overrides-applied form. The `artist-name-overrides.xml` override `<Artist canonical="Jay-Z" variant="JAŸ-Z" />` now fires correctly at scan-ahead time.

---

## 2026-06-27 - CountAlbumSongs library-side memoization

`CountAlbumSongs` was calling `Directory.GetFiles(albumFolder)` once per routing call. When a batch contains multiple songs from the same album (e.g. 6 Blueprint tracks), and dry-run calls `GetDestDir` twice per file (preview + actual routing), this became 12 identical filesystem scans for the same album. `_albumLibraryCountCache` (artist+album -> count) reduces this to one scan per unique pair per integration run.

---

## 2026-06-27 - Parallel XML reads in BuildMirrorIndex and Parser

`BuildMirrorIndex` (integration dedup phase): `Parallel.ForEach` over 5,000+ mirror XMLs using `ConcurrentDictionary`. `XmlDocument` is instantiated locally per iteration; aliases are read-only. Expected 3-6x speedup on multi-core hardware for the dedup indexing phase.

`Parser` (analysis cache miss): `Parallel.ForEach` over mirror XMLs using `ConcurrentBag<TrackTag>`. `TrackTag` construction is stateless per file. Non-XML corruption errors collected and surfaced after parallel phase completes. Progress dot output removed (parallel ordering is meaningless).

---

## 2026-06-27 - Fuzzy title normalization + batch tag cache for integration

`NormaliseTitleForDedup()` strips `(feat./ft./featuring X)` from both sides of the duplicate comparison before building or querying the mirror index. Library tags may not have been cleaned by TagFixer; incoming batch files have. Without this, "God's Plan (feat. Drake)" in the library silently missed "God's Plan" from the batch. `(Remix)` etc. are preserved as intended.

`RunScanAhead` now caches all raw tag data (`_batchTagCache`) as a side effect of its TagLib pass. `PreScanFiles` uses the cache instead of re-reading each file. For a 126-file batch, eliminates 126 TagLib reads (~1-3s on typical hardware). Falls back to direct read on cache miss.

---

## 2026-06-27 - TrackXML.Write uses atomic temp-file pattern

`TrackXML.Write()` now writes to `path + ".tmp"` first, then promotes atomically: `File.Replace` when overwriting (NTFS atomic), `File.Move` for first write. Temp file is cleaned up on any exception. Prevents a partially-written XML from corrupting the AudioMirror if the process crashes mid-write. Transparent to all callers; existing round-trip tests still green.

---

## 2026-06-27 - Album art stats verbosity reduced

Cover art histogram (up to 10 lines per run) collapsed to a single summary line: "Sub-800px: N | Non-square: M | Top dims: 800x800=3038, 1200x1200=1712...". Terminal output stays scannable; all actionable data (sub-threshold count, non-square count, top dimension buckets) is preserved on one line.

---

## 2026-06-27 - Two stale IDEAS.md items removed (already done)

"LibChecker detection via output capture is fragile" - already resolved: Program.cs line 151 reads `lc.IsClean` directly, never searches captureWriter string output. "TrackXML.Write should use LF line endings" - already resolved: TrackXML.cs uses `XmlWriterSettings` with `NewLineChars = "\n"` and `NewLineHandling = NewLineHandling.Replace`. Both items were documenting work completed in an earlier session.

---

## 2026-06-06 - CountAlbumSongs batch-side optimization

`CountAlbumSongs` was re-opening every NewMusic MP3 with TagLib# on each call (O(N*M) reads per integration run). `RunScanAhead` already does one full TagLib pass over the batch. Added `_scanAheadBatchAlbumCounts` (primaryArtist -> album -> count) populated during that existing pass. `CountAlbumSongs` now does an O(1) dict lookup for the batch-side count; the library-side (album subfolder scan) is unchanged. All tests green.

---

## 2026-06-06 - DecisionLog.cs dead code deleted

`DecisionLog` was unused in production since the 2026-05-25 TeeWriter refactor. The TIER-4 "decision XML analysis" feature it was built for was never scheduled and had no concrete next steps. Deleted: `DecisionLog.cs`, its `<Compile>` csproj entry, and `DecisionLog_SaveWithNoDecisions_ReturnsNull` test from `ManifestRunnerTests.cs`. 164 tests remain, all passing.

---

## 2026-06-06 - TrackXML IS-A design debt resolved

Replaced `internal class TrackXML : Track` (wrong inheritance - a serializer is not a Track subtype) with `internal static class TrackXML` containing two static methods: `Read(string path, TrackTag t)` and `Write(string path, TrackTag t)`. Uses `XDocument`/`XElement` (System.Xml.Linq) throughout - no XmlDocument, no XPath string lookups, no SetElementValue/GetElementValue wrappers. Null-safe element reads via `?.Value ?? ""`.

Call sites updated: `TrackTag` constructor now calls `TrackXML.Read(mirrorFilePath, this)` (one line) and `TrackXML.Write(mirrorFilePath, this)` (one line). `IntegrationTests.cs` and `ParserTests.cs` write calls updated to `TrackXML.Write(path, tag)`. `TrackXMLTests.cs` rewritten: now tests write-then-read round-trip (all 11 fields, special chars, missing elements) rather than testing internal class state.

Tests: 165 passing (0 failed). No force regen required - XML file format is identical.

---

## 2026-06-06 - ParseCache.Extract() replaces FieldCount magic constant

Added `internal static string[] Extract(TrackTag t)` to ParseCache. Returns all 12 cache fields in serialization order when given a real tag; returns an empty 12-element array for `null`. `FieldCount` is now derived from `Extract(null).Length`. `Save()` uses `string.Join(Sep, Extract(t))`. Adding a field in Phase 2.5 now requires one edit to `Extract()` - Save() and field-count validation in TryDeserialize() update automatically.

Two new tests: `Extract_NullArg_Returns12Elements` (schema length contract) and `Extract_ValidTag_ReturnsFieldsInCacheOrder` (pins positional order so drift from TryDeserialize is caught immediately).

---

## 2026-06-06 - Track.cs backing fields replaced with auto-properties

Removed all 12 explicit private backing fields from `Track.cs` and replaced with auto-properties. ~80 lines of boilerplate removed, zero behavior change. Tests all green.

**Issue 2 retraction:** The original IDEAS.md XML refactor included "delete the dead MP3-path branch in TrackTag" as Issue 2. This was a misdiagnosis. `Reflector.CreateFile()` writes the real MP3 file path to new and stale mirror files via `File.WriteAllText(fullMirrorPath, realFilePath)`. `TrackTag`'s constructor reads this, detects a valid file path, takes the TagLib# branch to extract tag data, then overwrites the mirror with XML. This is the primary path for all new files and any file whose tags are edited in Mp3tag. The branch is live - removing it would silently break all new integrations. Issue 2 removed from the refactor scope.

---

## 2026-06-06 - Album art schema lock + Phase 3: LibChecker sub-800 enforcement

**Schema lock:** Refined the flat `<CoverWidth>/<CoverHeight>` fields (Phase 1) into a nested `<AlbumCover><Count>N</Count><Width>W</Width><Height>H</Height></AlbumCover>` structure. Simple nested structure is sufficient; no redesign required. Strict parsing enforced in TrackXML. Analysis run 2026-06-03 data: 5653/5653 covers present, 97 non-square. Histogram: 800x800=3038, 1200x1200=1712, 1000x1000=431, 500x500=84, 600x600=41, 700x700=26.

**Phase 3 (Enforce):** LibChecker rule added - WARNING for any cover where `min(Width,Height) < 800`. 4 tests: low-res dirty, exact threshold (800) clean, non-square low-res dirty, Unknown-format clean. The ~151 sub-800 tracks in the real library will surface on the next analysis run; suppressible via exception config. 97 non-square covers also flagged - investigate in next analysis run (may be legitimate or fixable via mp3tag).

---

## 2026-06-03 - LibChecker genuine compilation exemption + deep-dive fixes

**LibChecker.CheckCompilationsFolder fix (TIER 1 blocker):** The rule was flagging ANY track in Compilations/ if the artist has an Artists/ folder. This produced 29 hits in the 2026-06-03 run (26x 2Pac + 2x Ice Cube + 1x Eminem) blocking the AudioMirror commit. Root cause: the routing logic correctly places tracks in Compilations/ when an album has 3+ distinct primary artists (genuine various-artist compilation), but LibChecker had no equivalent check - it applied the "has Artists/ folder -> misrouted" rule unconditionally.

**Fix:** `CheckCompilationsFolder` now first identifies genuine various-artist compilations (albums in Compilations/ with 3+ distinct primary artists) and skips them. Only tracks in single/dual-artist albums are flagged. New test: `LibChecker_GenuineCompilation_ArtistHasFolder_IsClean`. All tests passing.

**DevContext.md staleness:** Removed sentence claiming "User manually cleans tags in MP3Tag before integration (TIER 0 blocker)." TagFixer has been fully automated since May 2025.

---

## 2026-06-02 - Decided NOT to remove .md integration logs (TIER 4 evaluation)

Evaluated: `.md integration logs` vs `decision XMLs`. Finding: they're complementary, not redundant. .md logs contain TagFixer changes per file, duplicate resolution details, confidence report, and full narrative - information the structured decision XMLs don't capture. Decision XMLs are machine-readable routing records; .md logs are human audit trails. Both serve distinct purposes. Keep both. Item closed.

---

## 2026-06-02 - Library audit complete: LibChecker covers all Music-Library-Rules.md rules (TIER 3)

**Think-pass finding:** LibChecker already IS the audit tool. Running `analysis --force-regen` validates the entire library. Gap audit found one missing check: Compilations/ folder was never validated after being added as a routing destination.

**What was done:**
- `LibChecker.CheckCompilationsFolder()` - new check: if a track is in Compilations/ but its primary artist has an Artists/ folder, that's a routing mismatch (should be in Artists/{artist}/Singles/). Parallel to existing CheckMiscFolder "artist has folder but song in Misc" rule.
- `LibChecker` constructor - wired in after CheckMiscFolder.
- `LibCheckerTests.cs` - 2 new tests: clean case (no artist folder), dirty case (artist has folder but track in Compilations/).
- `Music-Library-Rules.md` - updated stale "LibChecker has an exception for Edition" note to reflect word-boundary fix.
- `IDEAS.md` - AUDIT sub-goal closed; ANALYSE and UNIFY extracted as separate TIER 4 items.

---

## 2026-06-02 - Reflector incremental: refresh stale XMLs when MP3 is newer (TIER 3)

Incremental Reflector was skipping any XML that already existed, even if the underlying MP3 had been modified in Mp3tag or another tool since the XML was last written. Stale tag data (wrong casing, old album name, etc.) would persist in reports until the next force regen.

**What was done:** `Reflector.CreateFile()` - added mtime comparison via new `IsStaleMirrorXml(mp3Path, xmlPath)` static method. When the MP3 is newer than its XML, the XML is overwritten with the real MP3 path, triggering a fresh tag read on this analysis run. Counter added to PrintStats ("XMLs refreshed: N", shown only when > 0). 2 new tests in ReflectorTests.cs.

Orphaned XML incremental cleanup was investigated simultaneously: force regen (`Directory.Delete(mirrorPath, true)`) already removes all orphaned XMLs completely. The incremental case is only relevant if a user deletes MP3s then runs incremental (not force regen) - this is not the expected workflow. Closed as won't-fix.

---

## 2026-06-02 - Audit libchecker-exceptions.xml: one bug-fix removed, all remaining genuine (TIER 4)

Audited all 12 exceptions. Found one bug-fix workaround: `<Exception unwanted="edit"><Album contains="Edition"/>` was needed because `Contains("edit")` incorrectly flagged albums with "Edition" (e.g., "Deluxe Edition", "25th Anniversary Edition") as having unwanted "edit" content. The other 11 exceptions are all genuine track-specific exemptions (official titles containing flagged words, intentional kept versions, special content).

**What was done:** `LibChecker.CheckProperty()` - added word-boundary matching (`\bedit\b`) for the "edit" unwanted check (same pattern already used for feat./ft.). "Edition" now never matches; "Radio Edit" still matches. Removed the broad `<Exception unwanted="edit"><Album contains="Edition"/>` from exceptions.xml. Added 2 tests. exceptions.xml header updated with audit date and clean status.

---

## 2026-06-02 - Thin verify.bat: aggregation moved into exe as `--verify` flag (TIER 3)

verify.bat was doing bat-level work: running `--test` and `--routing-manifest` in separate subprocess calls, capturing output to temp files, parsing "Results:" lines with `for /f tokens=2,4 findstr`, and combining pass/fail counts via `set /a`. 60 lines of bat logic.

**What was done:**
- `TestRunner.Run()` - added `out int passed, out int failed` params (return type stays bool)
- `ManifestRunner.Run()` - added overload with out params; original signature delegates to it
- `Program.cs` - added `--verify <manifestPath>` handler: runs TestRunner, short-circuits on unit fail, runs ManifestRunner, prints combined `[VERIFY] OK Total: X passed, Y failed` banner, exits with correct code
- `scripts/dev/verify.bat` - rewritten from 60 lines to 18 lines: build, run `--verify`, exit

---

## 2026-06-02 - Album art Phase 2 (Analyse): dimension stats in analysis report (TIER 2)

Phase 1 captured `<AlbumCover><Width>/<Height>` in every XML (via force regen). Phase 2 implements the dimension analysis.

**What was done:** `Analyser.PrintCoverArtStatistics()` - new section in the analysis report: tracks with cover vs. no cover vs. unknown format, non-square cover count, and a top-10 dimension histogram (e.g. "800x800: 3521 tracks"). Shows a warning when dimensions are missing (old-format XMLs pre-force-regen). Data is meaningful only after force regen migrates existing XMLs to the new `<AlbumCover>` format.

Phase 3 (Enforce: LibChecker rules for no-cover, non-square, below-threshold) remains in IDEAS.md pending threshold decision from real data.

---

## 2026-06-02 - Report path: print full absolute path for ctrl+click in terminal (TIER 2)

Markdown reports rendered correctly in `.md` files but the terminal printed a relative display path (`reports\2026\file.md`) which couldn't be ctrl+clicked in Windows Terminal.

**What was done:** `ReportWriter.Save()` - changed `displayPath` to `fullPath` in the console output. Full absolute path is now printed, making it immediately ctrl+clickable in Windows Terminal (opens in default `.md` viewer). One-line change.

---

## 2026-06-02 - Compilation album routing to Compilations/{album}/ (TIER 2)

Various-artist compilation albums (e.g. "Barbie The Album") were routing all tracks to Misc because no single artist owned the album. This was the highest-friction routing gap: every compilation track required manual intervention.

**What was done:**
- `Constants.cs` - added `CompilationsDir = "Compilations"`
- `MusicIntegrator.RunScanAhead()` - added album->distinct artists tracking in the existing TagLib scan pass; detects compilation albums (3+ distinct primary artists on same album) with no extra file reads
- `MusicIntegrator.GetDestDir()` - added `compilationAlbums` parameter (matches `newArtistFolders` pattern); routes compilation tracks with no artist folder to `Compilations/{albumName}/`; tracks where artist has a folder still route normally to `Artists/{artist}/Singles/`
- `RoutingTests.cs` - 3 new tests: no-folder routes to Compilations/, has-folder routes to Artists/Singles/, non-compilation album still Misc

**Result:** Compilation tracks are fully auto-routed. 155/155 tests passing.

---

## 2026-05-31 - Parser incremental cache (TIER 4)

Parser was reading every XML file on every analysis run regardless of whether anything had changed. On a 5600-track library on spinning disk this took ~105s and dominated the total run time.

**What was done:**
- `ParseCache.cs` - flat-file cache (pipe-delimited, `logs/parse-cache.txt`, gitignored). Validity check: compare cache `LastWriteTime` vs newest XML mtime via `DirectoryInfo.GetFiles()` - returns mtimes from the directory enumeration with no extra stat calls.
- `TrackTag`: internal 12-param constructor for cache loading (no I/O, just field assignment)
- `Parser`: cache hit path short-circuits all XML reads; cache miss falls through to full parse + saves cache
- `Constants.ParseCachePath`: `logs/parse-cache.txt`
- 8 `ParseCacheTests`: round-trip (all 12 fields), empty list, multiple tracks, missing/corrupt file, mtime logic with real temp files

**Results on 5653 tracks:**
- Cache miss (first run): 105.5s
- Cache hit (subsequent runs): 0.56s (**187x speedup**)
- Total analysis: 108.7s -> 3.6s

**Design decision:** all-or-nothing (any XML newer = full re-parse). Per-folder granularity would add complexity for marginal gain - the common case (no changes between analysis runs) is now essentially free.

---

## 2026-05-28 - scripts/dev/ reorganisation + verify.bat (TIER 2 - Phase 1)

Moved dev tools out of scripts/ root into scripts/dev/ so the root stays user-facing only. Added verify.bat as Claude's one-step build+test command (clean exit codes, no interactive prompt).

**What was done:**
- `build.bat` and `test.bat` moved to `scripts/dev/` with corrected `..\..\` relative paths
- `verify.bat` (new): calls build.bat then `--test`, exits 0/1, no cmd /k - replaces two-command sequence
- `launch.bat` updated to call `dev\build.bat`
- CLAUDE.md paths updated throughout; stale test counts removed (rule: never write exact counts in any doc)

**Why it mattered:** Dev tools mixed with user-facing launcher caused confusion. verify.bat gives Claude a single command with a single exit code - unambiguous pass/fail. Phase 2 (moving menu logic into Program.cs) remains in IDEAS.md TIER 2.

---

## 2026-05-28 - Automated test infrastructure: inline test runner, 19 TagFixer tests (TIER 1 - Session 1)

Added a complete inline test suite for TagFixer's pure string-manipulation logic. 19 tests, all passing. No xUnit - old-style csproj + no VS test runner in the workflow made a DIY approach strictly better.

**What was built:**
- `Code/Tests/Assert.cs` - 20-line assertion class (Equal, True)
- `Code/Tests/TestRunner.cs` - reflection-based runner, prints [PASS]/[FAIL] per test, exits code 0/1
- `Code/Tests/TagFixerTests.cs` - 19 tests across 5 functions
- `--test` CLI flag in Program.cs - bypasses TeeWriter/logging, runs tests and exits
- `scripts/test.bat` - builds then runs `AudioManager.exe --test`

**Tests cover:**
- `RemoveParentheticals` (5): feat., ft., Explicit, Album Version, plain title
- `StripAlbumSuffixes` (4): Deluxe Edition, Remastered, year suffix, plain album
- `ExtractAndFixArtists` (4): basic extraction, `&` split (Perry Como class), no duplicates, `of BandName` skip
- `ShouldFixGenre` (3): Musivation artist missing genre, already has genre, normal artist
- `DetermineGenre` (3): Akira The Don, Loot Bryon Smith, other artist

**Why it mattered:** Tag mutation regressions (Perry Como 4-artist bug, TMRWNITE casing) were only discovered days later during integration dry runs. Tests now catch these in < 1 second at build time. Deleted `StatisticTests.cs` (wrong scope, never wired up, stubs only).

---

## 2026-05-31 - AudioMirrorCommitter re-enabled with trigger modes (TIER 3) + IDEAS/CLAUDE.md housekeeping

**AudioMirrorCommitter (TIER 3):** Auto-commit is now live. Added `CommitTrigger` enum (AnalysisForceRegen / AnalysisIncremental / Integration). TryCommit() now:
- Skips silently for incremental analysis (stale XMLs possible)
- Auto-commits for force regen + clean LibChecker
- Auto-commits after clean post-integration validation
- Never pushes (user pushes manually per policy)
All safety gates preserved: LibChecker must be clean, files must have changed.

**Housekeeping:** TIER 1 ACTIVE FOCUS note updated (routing tests complete). Blank lines from deleted IDEAS items removed. CLAUDE.md verify command updated to include `--no-pause` and count note.

---

## 2026-05-31 - Album art Phase 1 + dupe UX + verify.bat manifest integration

**Album art Phase 1 - Capture (TIER 2):** `<CoverWidth>` and `<CoverHeight>` fields now written to every AudioMirror XML during regen. Pure byte-level JPEG/PNG dimension parser in TrackTag (no System.Drawing dependency). Existing XMLs get dimensions populated on next force regen. Enables Phase 2 (histogram analysis) without any further code changes.

**Dupe routing UX clarity (TIER 3):** [L] option now reads "Delete library copy (keep new file - will be routed in the next step)". Eliminates confusion about where the kept file ends up.

**verify.bat now includes manifest runner:** `verify.bat --no-pause` runs build -> unit tests -> routing manifest. All 39 assertions checked in one command (30 unit tests + 9 manifest tests).

---

## 2026-05-31 - JSON routing manifest: --routing-manifest flag (TIER 2)

`AudioManager.exe --routing-manifest <path>` validates GetDestDir routing against a JSON manifest without any real MP3 files or NewMusic folder. Each manifest entry specifies `artist, title, album, genres (optional), scenario (label), expectedDest` (relative to Audio root). Exit 0 if all pass, 1 if any fail.

**Implementation:** `ManifestRunner.cs` - simple regex-based JSON parser (no external deps), scan-ahead computed from batch counts in the manifest, uses real `MusicIntegrator(testLibraryPath)` constructor with `Constants.AudioFolderPath` to check real library structure without running the pipeline.

**Initial manifest** `test-fixtures/routing-manifest.json` - 9 scenarios all passing:
- Unknown artist -> Miscellaneous Songs
- Musivation/Motivation genre routing
- Scan-ahead: 3 songs by new artist -> Artists/Singles/
- 1-2 songs by new artist -> Miscellaneous Songs (below threshold)

**Limitation:** CountAlbumSongs does not scan the manifest batch, only the real library. Album subfolder tests (2+ songs -> album/ instead of Singles/) still require the unit test approach (AddAlbumFiles fixture).

---

## 2026-05-31 - --json-output mode + album tag inheritance (TIER 2/3)

**`--json-output` flag (TIER 2):** `AudioManager.exe integrate --dry-run --json-output` writes `logs/routing-{timestamp}.json` with structured routing decisions. Schema: `{filename, artist, title, album, destination, reason, isNewFolder, status, inBatchDuplicate, tagChanges[]}`. Additive - standard text output unchanged. Enables Claude to parse routing decisions programmatically for automated assertions. `LogEntry` extended with `Reason`, `IsNewFolder`, `InBatchDuplicate` fields (also captured in real-mode runs for future log enhancement).

**Album tag inheritance (TIER 3):** In `PreScanFiles`, if album tag is "Missing" and the file is in an immediate subfolder of NewMusic, the subfolder name is used as the album fallback. Runs before TagFixer simulation so suffix stripping applies. Fixes: tracks with missing album tags but grouped under an album folder would otherwise all route to Singles regardless of batch count.

---

## 2026-05-31 - NewMusic cleanup + RoutingConfidence enum removed (TIER 2/3)

**NewMusic cleanup (TIER 2):** `MusicIntegrator.CleanupNewMusicFolder()` added. Runs after each integration (dry-run shows preview, real mode deletes). Gate: if any files remain, warns and skips. Only removes empty subdirectories; leaves root NewMusic folder. Eliminates the manual cleanup step needed after May 2026 batch.

**RoutingConfidence enum removed (TIER 3):** Replaced `out RoutingConfidence confidence` with `out bool isNewFolder` in `GetDestDir()`. Removed ~80 lines of dead Uncertain-path code from the routing loop (Misc has routed as Certain since 2026-05-25). Display: `[AUTO - new folder]` replaces `[AUTO (likely)]` for scan-ahead promoted artists. All 30 tests passing.

---

## 2026-05-31 - Thin bats Phase 2: interactive menu moved into Program.cs (TIER 2)

launch.bat shrunk from 83 to 16 lines - now just builds and launches the exe. All menu logic moved into Program.cs.

**What changed:**
- `PromptMode()` expanded from 2 options to 3: Analysis / Analysis (Force Regen) / Integrate. Force Regen prompt eliminated as a separate step.
- Interactive Integrate path: Program.cs now runs dry-run internally, shows preview, then prompts "Proceed with real integration? [y/N]" before the real run. One exe invocation, one log file.
- `PromptForceMirrorRegen()` removed - collapsed into the main menu.
- `launch.bat` now: build + `AudioManager.exe` (no args) + timing. Zero menu logic.
- CLAUDE.md Build/Run section updated.

Pre-integration gate runs once (not twice as before when launch.bat ran two separate exe calls).

---

## 2026-05-31 - Test coverage expansion + bat --no-pause consistency (TIER 2)

**Routing tests expanded from 6 to 11:**
- `Routing_MotivationGenre_RoutesToMotivation` - genre routing to Motivation/
- `Routing_ExistingArtist_TwoAlbumSongs_RoutesToAlbumSubfolder` - album threshold at exactly 2 songs (the minimum)
- `Routing_ExistingArtist_OneAlbumSong_RoutesToSingles` - 1 album song is below threshold -> Singles
- `Routing_ExistingArtist_MissingAlbum_RoutesToSingles` - album="Missing" tag -> Singles
- `Routing_ExistingArtist_AlbumMatchesArtistName_RoutesToSingles` - album == artist name -> Singles (no distinct album)

**Test logging:** TestRunner now writes `logs/test-{timestamp}.log` alongside console output. Debugging test failures mid-session no longer requires a manual re-run.

**bat --no-pause consistency (TIER 2 QUICK WIN):** All bats now support the same two-mode contract: no args = `cmd /k` (human mode, window stays open); `--no-pause` = clean `exit /b` (Claude mode, no blocking). Affected: test.bat, verify.bat, launch.bat (which now passes `--no-pause` to build.bat internally). CLAUDE.md note updated.

Total suite: 30 tests, all passing.

---

## 2026-05-28 - Automated tests: routing correctness (GetDestDir) - Tier 1 complete

Added 6 routing correctness tests covering `GetDestDir()` - the highest-risk module (wrong routing = files moved to wrong library location).

**What was built:**
- `_libraryPath` field on MusicIntegrator, initialized from `Constants.AudioFolderPath` in production. Test constructor `MusicIntegrator(string testLibraryPath)` bypasses the pipeline entirely.
- `GetDestDir` and `RoutingConfidence` promoted to `internal` so tests can call them directly.
- `CountAlbumSongs` uses `_libraryPath` so fixture files are found correctly.
- `Tests/RoutingFixtures.cs` - `CreateLibraryFixture`, `AddAlbumFiles`, `Cleanup` helpers using temp dirs.
- `Tests/RoutingTests.cs` - 6 tests: existing artist->Singles/, existing artist+3 album songs->album subfolder, no artist folder->Misc, Musivation genre->Musivation/, scan-ahead new artist->Singles/, scan-ahead new artist 1 song->Singles/.

**Why it mattered:** Routing regressions (artist routed to wrong folder) were only catchable via dry run + manual review. Tests now catch them in < 1 second. Total suite: 25 tests, all passing.

---

## 2026-05-28 - Parked: three post-integration bugs from May 2026 run (deferred to next integration)

Removed from IDEAS.md 2026-05-28 - next integration is far off, will reassess if they surface then.

1. **Post-integration validation should force full AudioMirror rebuild** - non-force regen leaves stale XMLs; force regen post-integration clears them. Workaround: run force regen manually after integrating.
2. **LibChecker "version"/"bonus" regex fires on legitimate qualifiers** - `Extended Version`, `Bonus` flagged incorrectly. Workaround: `config/libchecker-exceptions.xml` (Joyner Lucas; Ashanti and Shaboozey already added).
3. **Integration should prompt for missing Album tag before integrating** - would have caught Dolly Parton - The Johnny Carson Show. Manual check until this is implemented.

---

## 2026-05-26 - May 2026 batch integration retrospective

Integrated 127 files. Routing was correct (97 Artists + 22 Misc + 8 Musivation). Tag fixes all correct, no unexpected filenames. 5 duplicates all recommended [L] (album over single - correct policy).

**Post-integration LibChecker was NOT clean.** Three issues surfaced after the run:
1. Stale XMLs from integration file moves - force regen cleared all 8 false duplicate pairs + 3 "artist-in-Misc" warnings + 1 stale Akira The Don path. Root cause: integration runs AudioMirror with `Recreated: False`; stale XMLs remain for moved files. Fix: always force full rebuild in post-integration validation (added to TIER 1).
2. LibChecker "version"/"bonus" regex fired on `Joyner Lucas; Ashanti - Fall Slowly (Extended Version)` and `Shaboozey - Chrome (Bonus)` - legitimate qualifiers that should not be flagged. Workaround: added to exceptions.xml. Fix in TIER 1.
3. `Dolly Parton - The Johnny Carson Show` missing Album tag - not caught before integration. Fix: TagFixer should halt integration on missing Album tag (TIER 1 feature).

**Key learning:** Dry runs run against existing library state and cannot predict stale XMLs produced by integration file moves. Post-integration validation must always force full AudioMirror rebuild - incremental mode is insufficient.

---

## 2026-05-26 - Batch artist casing fixes for May 2026 integration (config, no rebuild)

Added 5 entries to `artist-name-overrides.xml` to prevent title-case corruption on artists in the batch:
- `TMRWNITE` - all-caps artist; would have become `Tmrwnite` -> new Artists folder with wrong casing
- `OutKast` - mixed-caps; would have become `Outkast` -> LibChecker would flag existing `Artists\OutKast\` folder mismatch
- `The Kid LAROI` - mixed-caps; would have become `The Kid Laroi` -> LibChecker flag on existing folder
- `FIFTY FIFTY` - all-caps K-pop group; would have become `Fifty Fifty` -> aesthetically wrong
- `Terence McKenna` - internal cap in "Mc"; would have become `Terence Mckenna` -> filename vs People subfolder mismatch

Root cause: `ExtractAndFixArtists` applies `ToTitleCase(toLower())` to all artists not in the overrides XML. Config is loaded at runtime so no rebuild is needed for override additions. Pre-integration dry run confirmed: LibChecker Clean, all 127 files correctly routed.

---

## 2026-05-26 - Claude dry-run autonomy + output quality + in-batch duplicate detection (TIER 2)

Three causally-linked quality improvements delivered in one session:

**Claude autonomous dry-run (`--no-input` flag):** Added `--no-input` CLI flag so Claude can run `integrate --dry-run --no-input` without blocking on interactive prompts. Duplicate resolver auto-accepts the recommended key (K if no recommendation), error halts skip `ReadKey`, Uncertain routing path (dead code) also guarded. CLAUDE.md updated: analysis and dry-run are safe for Claude to run; real integration remains user-only. Payback: Claude can now verify routing fixes by actually running the tool, not just reasoning about code.

**Dry-run output quality pass (3 improvements):**
- Sort routing output by destination path - tracks from the same artist/album now cluster together, making routing anomalies visible by visual grouping
- Routing reason for Misc now shows full decision chain: `no artist folder; N in batch + M in library = T total, below threshold 3 -> Misc`
- `[DRY RUN]` lines now flush-left, aligning with `[AUTO]` routing lines

**In-batch duplicate detection:** Before routing begins, all NewMusic files are grouped by normalised artist+title. Any two sharing a key get `[WARN: IN-BATCH DUPLICATE]` prepended to their routing block in the sorted dry-run output (appears adjacent due to sort). Non-blocking flag-only; lets user delete duplicates before real integration. Also shown in real mode as safety net.

---

## 2026-05-26 - RunScanAhead Sources/ threshold (TIER 1)

`RunScanAhead` already counted batch + Misc tracks but missed `Sources/` tracks, so artists with a Sources/Films song (e.g. Common in Smallfoot) plus 2 batch songs still routed to Misc (2 < 3 threshold). Fix: also scan `AudioMirror/Sources/**/*.xml` by primary artist and include in the total. Sources tracks count toward promotion but are NOT migrated - they stay in Sources/ with their original classification. Scan-ahead note updated to show the breakdown (e.g. "2 in batch + 1 in Sources = 3 total -> new Artists/Common/").

---

## 2026-05-26 - TagFixer compound artist idempotency (TIER 1)

`ExtractAndFixArtists` was adding compound "A & B" featured-artist strings as a single entry even when "A" and "B" were already individually present in the artist field. Example: Perry Como track with `(with Mitchell Ayres and His Orchestra & The Ray Charles Singers)` in the title produced 4 artist entries instead of 3.

Fix: split extracted featured-artist groups on `" & "` before the existing-artist check. Each component is added individually only if absent. The `&` in title parentheticals is always a separator, never part of a name, so this split is always safe. The `.Distinct()` downstream remains as a final safety net. Artist field mutations are now idempotent for compound forms.

---

## 2026-05-26 - TagFixer filename guard for empty artist/title after sanitisation (TIER 3a)

If artist or title tag was empty (or contained only illegal filesystem characters), `SanitiseFilename` returned an empty string and the rename produced `" - TITLE.mp3"` or `"ARTIST - .mp3"`. Guard added to both real and dry-run paths: if either sanitised part is empty, log `[WARN] Rename skipped: empty artist or title after sanitisation`, keep original filename, continue. The tag changes (TCMP, genre, etc.) still apply - only the rename is skipped.

---

## 2026-05-26 - Fix ATD scan-ahead false positive + left-align input prompt (dry run bugs)

**ATD scan-ahead false positive:** `RunScanAhead` checked only `Artists/{artist}/` folder existence to decide whether to promote an artist. Akira The Don has no `Artists/Akira The Don/` folder (lives in `Musivation/Akira The Don/`), so it appeared as "new Artists/Akira The Don/" in every dry run - wrong. More critically, if ATD ever had songs in `Miscellaneous Songs/`, `RunMiscMigration` would move them to `Artists/Akira The Don/Singles/` (wrong path) - LibChecker would then fire. Fix: also check `Directory.Exists(Musivation/{artist}/)`. If the artist already has a Musivation folder, they're excluded from scan-ahead entirely.

**Input prompt alignment:** `ReadMenuKey()` used `"             > "` (13-space indent), rendering far right of the options. Changed to `"> "` - fully left-aligned.

---

## 2026-05-26 - Fix ~1 minute silent hang during duplicate detection (TIER 2 bug)

`PreScanFiles()` called `FindDuplicateInMirror()` once per batch file. Each call did `Directory.GetFiles(...*.xml, AllDirectories)` + XmlDocument.Load on every AudioMirror XML - O(mirror_size x batch_size). With a large mirror and a medium batch this produced a ~1 minute silent hang with no output, indistinguishable from a crash.

Fix: `BuildMirrorIndex()` pre-loads the full AudioMirror into a `Dictionary<string, string>` (normalised `primary\0title -> xmlPath`) once before the pre-scan loop. Prints "Indexing AudioMirror for duplicates (N tracks)..." so the user sees activity. `FindDuplicateInMirror` signature changed to accept the index; lookup is O(1). Total cost: O(mirror_size + batch_size) instead of O(mirror_size x batch_size).

---

## 2026-05-26 - (Single Version) and (Radio Version) stripped from title field (TIER 3)

`RemoveParentheticals` now strips `(Single Version)` and `(Radio Version)` from track titles. `(Radio Version)` was already handled in `StripAlbumSuffixes` for the album field but not the title field. Batched as two patterns in one change per IDEAS.md.

---

## 2026-05-26 - Unified integration gate + (with X) tag fixer (TIER 2)

**Unified integration gate:** `launch.bat` options 3 (Dry Run) and 4 (Real) collapsed into a single "Integration" option. Dry run always runs first and displays full output; user must explicitly type `y` to proceed to real integration (default is N). The old "Are you sure?" prompt is removed - seeing the dry-run output IS the review. Enforces the safety property: you cannot move files without having read the routing decisions on screen.

**`(with X)` tag fixer:** Three current-batch tracks used `(with X)` instead of `(feat. X)` - Barbie World (with Aqua) and two Perry Como tracks with long orchestral credits. TagFixer now strips `\(with\s+[^)]+\)` from title/album via `RemoveParentheticals` and extracts the collaborator name into TPE1 via `ExtractAndFixArtists` - identical pattern structure to existing `(feat. X)` handling. Perry Como's orchestral credits will appear as long TPE1 strings; check post-dry-run for new-folder routing side effects; add to `artist-name-overrides.xml` if needed.

---

## 2026-05-25 - Comprehensive artist casing overrides (TIER 2 audit)

Periodic casing audit: scanned all library folders against what `ToTitleCase(a.ToLower())` produces. Found 35 artists whose canonical names can't be reproduced by default title-casing (all-caps acronyms, hyphenated names, camelCase, digit-prefixed, all-lowercase styles). Added all 35 to `config/artist-name-overrides.xml`. Without these, integrating a new track by e.g. POLO G would write "Polo G" to the tag, causing LibChecker to flag a folder name mismatch. Codebase audit (docs/comments) was clean - no rules were missing from documentation.

---

## 2026-05-25 - Auto-migrate Misc songs on artist promotion (TIER 2)

When scan-ahead detects an artist crossing the 3-song threshold (triggering a new `Artists/` folder), any existing songs for that artist in `Miscellaneous Songs/` are now automatically migrated to `Artists/{artist}/Singles/` at the end of the integration run - no manual intervention required.

Works in both dry-run (shows `[DRY RUN] Would move:` lines) and real mode (moves files). The scan-ahead note updated from "need manual migration" to "will be auto-migrated". A `[WARN]` is printed if a library file is missing (stale AudioMirror edge case).

Implementation: `RunScanAhead` now tracks Misc XML file paths per artist (not just counts) and populates `_miscMigrationCandidates` for promoted artists. `RunMiscMigration()` runs post-routing in both modes.

---

## 2026-05-25 - TagFixer genre extension + TeeWriter embedded newline fix (TIER 2)

**TagFixer genre extension:** `ShouldFixGenre` and `DetermineGenre` extended for two cases:
- **Loot Bryon Smith** - now identified as a Musivation artist; genre set to "Musivation" automatically (same path as Akira The Don). TagFixer is now 100% comprehensive for known Musivation artists.
- **Generic Motivation tracks** - if a track's genre contains "Motivation" but isn't exactly "Motivation" (e.g. "Motivational", "Motivation Music"), TagFixer normalises it to "Motivation". `DetermineGenre` returns "Motivation" for all non-Musivation cases (the only other trigger path).

**TeeWriter embedded newline fix:** `WriteLine(string)` previously wrote the full string as one unit to the file, so `"\nFoo"` produced a timestamped blank line + an untimestamped "Foo" line. Fix: if value contains `\n`, delegate to `WriteCharToFile` char by char - same logic as `Write(char)`. A shared `WriteCharToFile` helper extracted to remove duplication. Verified in `run-2026-05-25_155408.log`: every content line has `[HH:mm:ss]` prefix.

---

## 2026-05-25 - Integration UX cleanup: Misc auto-route + duplicate output removed (TIER 2)

Two small fixes from the 2026-05-25 dry run that surfaced confirmation fatigue and redundant output:

1. **Misc auto-route:** Changed `RoutingConfidence.Uncertain` to `RoutingConfidence.Certain` in the Misc fallback branch of `GetDestDir()`. Misc-bound tracks (no artist folder found) now print `[AUTO]` and route without a Y/N prompt. Misc is non-destructive; the dry-run preview is the safety gate.

2. **Duplicate "Finished!" removed:** `Program.cs` was printing `Console.WriteLine("\nFinished!\n")` after integrate/tagfix runs, but `MusicIntegrator` already prints a `=== Finished ===` block. The redundant Program.cs line is removed. Analysis mode is unaffected.

---

## 2026-05-25 - Unified run log with timestamps (TIER 2)

Replaced two separate log files (`integration-*.md` markdown summary + `decisions-*.xml` structured routing log) with a single `run-YYYYMMDD-HHmmss.log` per integration run. All console output is captured with a `[HH:mm:ss]` timestamp prefix on every line in the file; console output itself is unchanged (no timestamps visible on screen).

Implementation: `TeeWriter` gained an `addFileTimestamps` flag and `atLineStart` tracking. When enabled, `Write(char)` and `WriteLine(string)` prefix file writes with the current time. `Program.cs` enables this flag for integrate mode and renames the log from `integrate-*.log` to `run-*.log`. `MusicIntegrator` had `SaveLog()`, `DecisionLog` usage, and `PrintTimestamped()` all removed - the TeeWriter stream capture replaces them all.

Routing decisions (previously in `decisions-*.xml`) are now captured as part of the console stream. DecisionLog class is retained but unused; can be deleted if decision XML analysis (TIER 4) is never pursued.

Known edge cases logged in IDEAS.md: embedded `\n` in strings produces an untimstamped content line; investigate any console output not captured by TeeWriter.

---

## 2026-05-25 - Combined per-file summary: tag changes inline with routing (TIER 2)

Previously: TagFixer printed a standalone "Tag Fix Summary" block (all files at once), then MusicIntegrator printed routing per file separately. User had to mentally join two separate lists to understand what happened to each file.

Now: routing block shows everything in one pass per file:
```
[AUTO] Artist - Song
 > Title: "Song (feat. X)"  -> "Song"
 > Artists: "Artist"  -> "Artist;X"
 Route: Artist / Singles
 Reason: Artist folder...
 Path: Artists/...
```

Implementation: `TagFixer` exposes `FileChanges` (dict: filename -> change list). Filename-matching handles the rename case: both original and post-rename names are registered as keys so lookup works regardless of whether TagFixer renamed the file. `MusicIntegrator.PreScanFiles` reads this dict and populates `LogEntry.TagChanges`, which the routing display reads. TagFixer's per-file summary block is replaced with a single count line. Works identically for dry-run and real-run modes.

---

## 2026-05-25 - Progress indicators for scan-ahead and parser (TIER 2)

Two silent phases now show user-visible progress. Both are output-only changes.

**Scan-ahead (MusicIntegrator.cs - RunScanAhead):** Previously printed nothing while reading batch files and scanning the entire Misc AudioMirror folder (could be hundreds of XML files). User reported "thought program hung." Now prints: `Scan-ahead: reading N file(s)... checking Misc (N)... done.` before any computation.

**Parser (Parser.cs):** Previously 38 seconds of silence between "Parsing audio metadata..." and "Tags parsed: 5516" in analysis mode. Now prints a dot per 10% of the library, giving `Parsing audio metadata...........` on one line. Uses `dotInterval = max(1, total/10)` so it scales to any library size.

Impact: the two longest silent phases in the integration + analysis workflow now show forward motion. Eliminates "is it frozen?" uncertainty.

---

## 2026-05-25 - Output formatting refinements (TIER 2 quick win)

Five sub-items from Feedback823 dry-run review. All display-layer changes, no logic touched.

1. **Removed duplicate header** - `[Step 1] Fixing tags...` in MusicIntegrator duplicated TagFixer's own `Fixing music tags...` header. Removed the outer one.
2. **Moved count summaries after per-file table** - TagFixer's "Files processed" and "Fixed/Skipped" now appear after the per-file results table (before timing), not before. Consistent with routing section layout.
3. **Removed "--- Per-file results ---" labels** - Unnecessary dividers removed from TagFixer summary and confidence report.
4. **Blank line before routing timing** - Added visual separation between "Routed: N | Skipped: N" and "Routing - time taken:".
5. **Combined log saved lines** - "Log saved: X" and "Decision log saved: Y" merged into single "Logs saved: X, Y". `SaveLog()` and `DecisionLog.Save()` now return paths (null on failure/empty) instead of printing directly. Commit: ad0ee85.

---

## 2026-05-25 - Routing proposal UX: three quick wins (TIER 1 closeout)

All 3 sub-items were already implemented across commits 4dd2e0b9 and f7a4f526 but never closed out. Verified and closed in this session.

**(1) Split `Proposed:` into readable + filesystem path** - The Uncertain block now prints `Proposed: {routeSummary}` (short readable summary, e.g. "Akira The Don / Singles") on its own line, then `Path: {relativeDest}` (full filesystem path) on the next. Previously was one long `Proposed: full\path` line. Commit: 4dd2e0b9.

**(2) `Reason` field explains WHY** - All `GetDestDir()` reason strings explain the logic ("Artist folder; 3 songs from album -> album subfolder", "No artist folder found in library") rather than restating the destination ("Akira The Don -> Singles"). ATD reason strings in particular were cleaned up. Commit: 4dd2e0b9.

**(3) Concise proposal positioning fixed** - Old `-> Artist / Folder` summary line was renamed to `Proposed:` and reordered. The Uncertain block now reads top-to-bottom: WHERE-short (Proposed), WHERE-full (Path), WHY (Reason). Previously the summary line appeared before `Proposed: path`, feeling out of order. Commit: 4dd2e0b9.

Impact: Uncertain routing decisions are now scannable at a glance. User can read summary, verify full path, understand logic without parsing a long `Proposed:` line.

---

## 2026-05-08 - Report table formatting fix (TIER 2 quick win)

Fixed plain-text column format in analysis reports. Converted fixed-width spacing to markdown pipe-delimited tables with proper header separators. All statistics tables (Artists, Genre, Year, Decade) now render correctly on GitHub and other markdown viewers. Changed `Statistic.PrintColumns()` to emit markdown format when isHeader flag set, and inserted blank line + separator row after each table header. Generated test stubs for markdown validation. Commits: 5718676, c3b52ba.

---

## 2026-05-08 - Auto-routing for known cases (TIER 1 target)

Added `RoutingConfidence` enum (`Certain`/`Likely`/`Uncertain`) to `GetDestDir()`. Routing loop now skips Y/N prompt for Certain/Likely routes and logs `[AUTO]` / `[AUTO (likely)]` to output. Uncertain routes (Misc fallback - no artist folder found) still show full prompt. Confidence assignment: existing artist folder and all ATD/genre-override routes = Certain; scan-ahead new artist folder = Likely; Misc = Uncertain. All decisions including auto-routed ones logged to XML with confidence prepended to reason. Impact: typical integration batch auto-routes all known artists without any Y/N presses; only truly new artists hit the prompt. Commit: 1911640.

---

## 2026-05-08 - All TIER 1 prerequisites complete (2.1 + 2.2 + quick wins)

**TagFixer: character validation & sanitization pass (TIER 1 prerequisite 2.2)**
Applied `SanitiseFolderName()` to all artist and person-name path construction sites (6 sites across `RunScanAhead`, `GetDestDir`, `CountAlbumSongs`, `CountAkiraTheDonPersonSongs`, `CountAkiraTheDonPersonAlbumSongs`). Artist names were previously used raw in `Path.Combine()` - a name like "AC/DC" would silently break path construction since `/` is a path separator on Windows. `SanitiseFilename` already uses `Path.GetInvalidFileNameChars()` which is comprehensive (covers `?`, `/`, `\`, `:`, `*`, `<`, `>`, `|`, `"`). Also fixed dry-run simulation in `PreScanFiles`: album was cleaned with `RemoveParentheticals` only, not `StripAlbumSuffixes`, so dry-run routing predictions for suffix-corrupted tracks were wrong while real-run was correct. Both now consistent. Remaining scope: tag validation of 5531 library tracks via user-run Analysis (LibChecker surfaces any metadata issues). Commit: 16ff2c9.

---

## 2026-05-08 - Blocker B2 fix + two quick wins

**TagFixer: Comprehensive suffix stripping (TIER 1 prerequisite 2.1)**
Added `StripAlbumSuffixes()` called after `RemoveParentheticals()` for the album field only (not title). Strips edition markers `(Deluxe Edition)`, remaster tags `(2011 Remaster)`, version qualifiers `(International Version)`, bonus content `(Bonus Tracks)`, and year suffixes `(2019)`. Root cause of Blocker B2 (Shaggy "It Wasn't Me" routed to wrong album subfolder): folder-name suffix leaked into album tag, making GetDestDir() see a unique album and route to subfolder instead of Singles. Fix prevents that triple-failure chain (tag corruption -> wrong routing -> LibChecker false positive). Commit: 630a7a7.

**TagFixer output formatting: consistent blank-line separators**
Added `Console.WriteLine()` after `[SKIPPED]` and `[ERROR]` entries in the per-file report to match existing spacing after `[FIXED]` entries. Prevents SKIPPED/FIXED blocks from running together in output. Commit: 41f851b.

**Mark historical workflow docs as frozen**
Created `docs/Historical/WorkflowExecution-2026-04-26/README.md` with FROZEN header to prevent future maintenance attempts on stale pre-execution planning docs. Commit: 3a4ec61.

---

## 2026-05-07 - Stage 3C Retrospectives Complete

Comprehensive post-integration analysis completed for Stage 3C (April 26 - May 7, 2026, 11 days, 251 commits).

**Forensic Analysis:** `docs/Development/FEEDBACK-Stage3C-2026-05-07.md` (403 lines)
- Full timeline of execution, blocker discoveries, root cause analysis, pattern analysis, confidence assessment, process insights
- Key findings: 3-blocker cluster identified (illegal characters, artist casing, album suffixes), dry-run effective for normal cases but misses statistical outliers (edge cases found in real integration), scope evolution tracked (82 IDEAS updates = 33% of commits)
- Deliverable: TIER 2+ work prioritized by findings and validated by 502 real integration outcomes (Musivation batch, Stage 3C blocker fixes verified)
- Impact: All downstream TIER 2 items now cross-referenced to retrospective root cause analysis and evidence

**Claude Workspace Reflection:** post-mortem analysis archived privately by the maintainer (650 lines)
- Post-mortem analysis of Claude's performance during AudioManager Stage 3C (12 /dev-session invocations, 247 commits)
- Decision audit: validated decisions (character sanitization, artist casing overrides, routing logic, decision logging), partially validated decisions (album suffix stripping, dry-run methodology), unvalidated decisions (parser optimization, full library consistency)
- Rule system effectiveness: improvement loop broken (learnings captured in narrative but not converted to rules), TDD gate missing (2% test commits vs 30% expected), scope creep visible (82 IDEAS updates untracked mid-session)
- Recommendations: P0 (fix improvement loop in /dev-session), P1 (add TDD gate), P2 (add scope gate), P3+ (workspace cleanup and hardening)
- Impact: Identifies systematic improvement loop gap (narration without action defeats self-improvement). Sets priority for /dev-session skill enhancements (add enforced checkpoints for TDD, scope, rule capture)

**Integration Outcome:** Real integration attempt May 3 succeeded after blocker fixes applied May 5. 5531 songs verified clean post-integration. Decision logging enabled forensic analysis. Dry-run post-fix verified no regressions.

---

## 2026-05-05 (evening) - Scott Adams artist casing rule restored

**Blocker resolution:** Scott Adams title-casing rule was accidentally lost during the initial refactor to add `artist-name-overrides.xml` system. Rule existed in code comments but was not ported to the new config. Added `<Artist canonical="Scott Adams" />` to config/artist-name-overrides.xml. Clarified config file comment to document that override entries apply to both primary and featured (secondary) artists - one entry per artist covers all positions.

**Lesson:** When refactoring code that handles rules/config/overrides, audit existing rules BEFORE refactoring and ensure ALL are migrated to the new system. Silent data loss is serious.

---

## 2026-05-05 - TIER 1 complete: crash fix, TagFixer casing, LibChecker clean, first successful dry run

Three blockers from the 2026-05-03 partial integration crash resolved. First dry run post-fix completed with no errors.

**Blocker A - Integration crash: illegal characters in path**

`GetDestDir()`, `CountAlbumSongs()`, and `CountAkiraTheDonPersonAlbumSongs()` used raw `track.Album` values directly in `Path.Combine()` calls. Album tags can contain Windows-illegal characters (e.g. "WHAT IF?" contains `?`). Added `SanitiseFolderName()` helper in `MusicIntegrator` wrapping `Reflector.SanitiseFilename()`. Updated all four path-construction sites. Display strings (reason lines) still show the raw human-readable album name unchanged.

**Blocker B1 - Eels "soundtrack" LibChecker false positive**

Album "Useless Trinkets: B-sides, Soundtracks, Rarities and Unreleased" legitimately contains "Soundtracks". Added exception to `libchecker-exceptions.xml` matching `Artists contains "Eels"` + `Album contains "Useless Trinkets"`.

**Blocker B2 - Mike. single-song routing**

Confirmed not a routing logic bug - library already had 6 songs in `mike\the highs\` by the time the session ran. The failure was a transient artifact of the crash-interrupted partial run. Resolved once remaining songs were integrated.

**Bonus: TagFixer was title-casing "mike." to "Mike."**

`ExtractAndFixArtists()` applied `ToTitleCase(a.ToLower())` to all artists, silently converting "mike." to "Mike." on integration. This caused `CheckAlbumSubfolderRule()` to see only 1 song in the "Mike." group (the others were "mike."), falsely triggering the singles/album mismatch rule. Fix: added `config/artist-name-overrides.xml` with a `<Artist canonical="mike." />` entry and updated `ExtractAndFixArtists()` to use canonical casing from config instead of title-casing for listed artists. Two already-integrated "Mike." library files fixed manually via Mp3tag. Future integrations preserve "mike." correctly.

**Note on artist casing rules:** The override system (`artist-name-overrides.xml`) is the durable home for all artist casing rules going forward. During this refactor, some existing rules may have been missed - audit the codebase and HISTORY for any other artists that require non-standard casing (e.g. Scott Adams) and ensure they're migrated to config.

---

## 2026-05-03 - UX: Duplicate detection and output grouping (two interrelated items)

**Item A - Smarter [L] recommendation for ATD-style compilations**

`IsAlbumFolderCompilation()` previously collected only the primary artist from each XML, so ATD compilation albums (e.g. MEANINGWAVE MASTERPIECES V) were never detected - every track shares "Akira The Don" as primary. Changed to collect ALL artists from each XML (every semicolon-separated value), so the set now includes both ATD and the sampled person per track. For MEANINGWAVE V: ATD + 10+ different persons = 11+ distinct artists, easily crosses the threshold of 3. Traditional compilations (many different primary artists) still caught. The `libraryIsCompilation` flag in `BuildDupData()` was already wired to call `IsAlbumFolderCompilation()` - only the detection logic inside needed fixing. Updated dupReason text to match IDEAS.md spec.

**Item B - Consolidated duplicate execution outputs grouped before routing**

Step 3 (routing) previously executed D/L decisions inline within the routing loop, interleaving duplicate outcome messages with routing messages. User had to context-switch repeatedly. Refactored into two sub-passes: 3a iterates duplicate files and executes all D/L decisions (grouped output); 3b iterates all files and routes non-D/non-dry-run-L files. K files produce no output in 3a and fall through to routing in 3b. Real-mode L files have their library deletion executed in 3a (visible before routing begins) then are routed in 3b. Added `SkipRouting` flag to `DupData` to communicate 3a -> 3b. Both items compile and build clean.

---

## 2026-05-02 - Fix: plural "songs" when count is 1

Two reason strings in `MusicIntegrator.GetDestDir()` could produce "1 songs": the ATD Singles branch (`personSongCount < 3`, can be 1 or 2) and the Artists Singles branch (`albumCount < 2`, already had a `song(s)` workaround). Both fixed with inline ternary. All other count strings in the same method are guarded by `>= 2` or `>= 3` checks and were already correct.

---

## 2026-05-02 - UX: Add succinct routing summary line

Added `-> Artist / Folder` summary line above the full `Proposed:` path in the routing display block. `GetRouteSummary()` strips the top-level category folder (Artists, Musivation, etc.) and filename, then formats the remaining path as `A / B / C`. Special case: "Miscellaneous Songs" abbreviates to "Misc". User can scan the arrow line for quick Y/N; full path stays for diagnostics.

---

## 2026-05-02 - UX: Separator bars widened from 60 to 75 chars

`====` and `----` bars in MusicIntegrator.cs and TagFixer.cs extended from 60 to 75 characters. Long song titles (e.g. "WHAT YOU ARE LOOKING FOR IS WHAT YOU ARE") were overflowing the 60-char bars. Both bar types updated consistently via replace_all.

---

## 2026-05-02 - UX: Add Track + Album lines under In AudioMirror entry

Added `Track:` and `Album:` lines below the "In AudioMirror:" path in the duplicate detection block. Previously the library entry showed only the XML path; user had to parse folder segments to find the album. Now `ReadMirrorTrackInfo(xmlPath)` reads the XML directly and surfaces the same fields shown in the new file block. Both blocks now have matching information density. Falls back gracefully (lines omitted) if XML is unreadable.

---

## 2026-05-02 - UX: Remove redundant Track line + Separate Proposed from Reason

Two duplicate detection display cleanups in `MusicIntegrator.cs`.

**Remove redundant Track line:** The `Track: Artist - Title` line in the new file block was a repeat of what the `New file: Artist - Title.mp3` filename already showed. Removed the Track line. Album remains (it adds context the filename doesn't have).

**Separate Proposed from Reason:** `dupProposed` was bleeding justification into the action statement (e.g. "Delete NewMusic copy - already have this from 'Album'"). Now `Proposed` is a pure action ("Delete NewMusic copy, keep library" / "Delete library copy, keep new file" / "No version preference") and `Reason` carries the full justification. For the same-album case, the album name moved from Proposed into the Reason string so no context is lost.

---

## 2026-05-02 - UX: Same-song/same-album duplicate detection

When the new file is from the same album as the library copy, `MusicIntegrator` now detects this and immediately recommends [D] (keep library, delete NewMusic copy). Detection: `Path.GetFileName(Path.GetDirectoryName(duplicatePath))` gives the library album folder name; compared case-insensitively against `track.Album`. `dupProposed` shows the specific album name ("Delete NewMusic copy - already have this from 'Album'"). Takes priority over all other recommendation rules (single vs album, compilation vs album).

---

## 2026-05-02 - UX: Duplicate detection display overhaul (4 items)

Reworked the duplicate detection display block in `MusicIntegrator.cs` to match routing display quality.

**Layout (item 4):** Library context (AudioMirror path) comes first, then a blank line, then new file info as a grouped unit. User no longer has to mentally separate scattered fields.

**Corrected filename (item 5):** New file now shows the cleaned tag name (`Artist - Title.mp3`) instead of the raw original filename. Tags are already cleaned by TagFixer (real mode) or the dry-run simulation at this point, so this shows what will actually be stored.

**Proposed/Reason (item 2):** Added `Proposed:` line describing the recommended action (e.g. "Delete library copy, keep new file (album version preferred)") and `Reason:` line showing the rule that triggered it. Matches the context quality of routing proposals.

**Separator above options (item 3):** Added `----` separator line above the options line. Routing display always has `----` before and after options; duplicate display now matches.

---

## 2026-05-02 - Feat: Album preference rules in duplicate detection + DecisionLog logging

Extended duplicate detection with two album preference rules and decision logging.

**Rule 1 (existing, formalized):** Library has single + new file has real album -> recommend [L] (replace with album version). Was already in code but undocumented. Now has explicit reason string: "Library has single; new file is from album X - album preferred".

**Rule 2 (new):** Library has compilation track + new file has artist album -> recommend [L]. Detected via mirror path: if `relMirrorPath.StartsWith("Compilations\")`, the library copy is from a compilation. Artist albums are the definitive release. Example: library has "Changes" from a Various Artists compilation; new file is from 2Pac's Greatest Hits - recommend replace.

**newIsAlbum detection tightened:** Added `!track.Album.Equals("Missing")` guard to prevent false positives on tracks with no album tag.

**DecisionLog logging:** Duplicate decisions (D and L choices) are now logged to DecisionLog XML with reason. D logs "User kept library copy" (plus note if overriding a recommendation). L logs the preference rule reason, or "User chose to replace library copy" if no recommendation applied. K (keep both) is not separately logged - the downstream routing log entry is sufficient.

---

## 2026-05-02 - Fix: Dry-run now shows post-TagFixer (simulated) tags in routing blocks

Dry-run routing blocks were showing raw tags from disk, so `(feat. Artist)` would appear in the title, artist casing was uncorrected, and genre was unset - all misleading since real integration never sees these values (TagFixer cleans them first).

Fix: after reading raw tags in dry-run mode, MusicIntegrator now applies the same in-memory transformations as TagFixer before routing and display: `ExtractAndFixArtists` (feat. extraction + casing), `RemoveParentheticals` on title and album, genre assignment if needed. TCMP excluded (user trusts it). The simulation uses the original title for feat. extraction (correct order), then overwrites with cleaned values.

Result: dry-run is now a faithful preview of real integration. Routing proposals show the cleaned artist, cleaned title (no `feat.`), cleaned album, and correct genre - the same values that will be on disk when real integration runs. User can confirm tags and routing in one pass without cross-referencing the TagFixer output section.

Implementation: TagFixer helper methods (`RemoveParentheticals`, `ExtractAndFixArtists`, `ShouldFixGenre`, `DetermineGenre`) promoted from `private` to `internal static` so MusicIntegrator can call them directly.

---

## 2026-05-02 - Fix: ATD People/ routing now uses Singles/Album subfolder structure + artist casing fix

Two causally linked fixes applied together (ATD routing correctness depends on correct artist casing).

**Fix 1 - ATD People/ subfolder routing (TIER 0 BLOCKING):**
`GetDestDir()` was routing Akira The Don tracks directly to `People/{Person}/Song.mp3`, violating the Music-Library-Rules.md requirement for a subfolder before any song file. Fixed: when a person qualifies for their own People/ folder (3+ songs), the same album-vs-singles threshold logic used in the Artists/ path now also applies - if 2+ songs from the same album exist, routes to `People/{Person}/{Album}/`, otherwise routes to `People/{Person}/Singles/`. New private method `CountAkiraTheDonPersonAlbumSongs` counts songs for a given person+album combination (library + batch), mirroring the existing `CountAlbumSongs` method for the Artists/ path. Before this fix, all ATD tracks with 3+ person songs would land at wrong paths (missing the required subfolder level).

**Fix 2 - TagFixer compound artist name capitalization (TIER 1):**
`ExtractAndFixArtists()` was returning artist names without casing normalization. Example from real dry-run: "Akira The Don; Scott adams" where "adams" remained lowercase, producing wrong filenames and wrong People/ folder names in ATD routing (the two fixes are directly linked). Fix: added title-case normalization via `TextInfo.ToTitleCase(a.ToLower())` applied to each artist part independently before the `Distinct` call. Input "Akira The Don; Scott adams" now produces "Akira The Don; Scott Adams". Already-correct casing is preserved unchanged.

Both changes build cleanly.

---

## 2026-05-01 - Fix: Right-click in terminal no longer triggers Decline

Root cause: `Console.ReadKey(intercept: true)` immediately consumes any char arriving in stdin, including characters pasted by right-click in Windows Terminal. A clipboard containing "n" would silently fire the [N] Decline handler mid-routing - a data-safety bug.

Fix: replaced both `Console.ReadKey()` call sites in `MusicIntegrator.cs` with a new `ReadMenuKey()` helper that uses `Console.ReadLine()`. User must now type the key AND press Enter. Paste events appear visibly on screen and can be backspaced before submitting. Invalid input silently re-prompts with `> `.

Affected: duplicate resolution loop (D/L/K/Q) and routing confirmation loop (Y/N/Q).

---

## 2026-05-01 - UX: Blank lines between TagFixer output blocks

Added `Console.WriteLine()` after each `[WOULD FIX]` / `[FIXED]` block in the per-file results loop (`TagFixer.cs` lines ~192-203). Previously blocks printed back-to-back with no separation, making dry-run output hard to scan across 20+ files. One line change, confirmed working by user on real dry-run.

---

## 2026-04-29 Session 1 - UX: Remove folder picker, add Decline option

**UX FIX - Replace [N] Choose folder with [N] Decline:** Removed folder picker entirely. Previously, pressing [N] on routing proposal opened PickFolder() prompting user to type a destination path. This was "very bad" (user feedback) - most [N] presses mean "routing logic is wrong, not this specific folder." New flow: [N] simply declines file and leaves it in NewMusic for next run. Cleaner, faster, eliminates unnecessary user prompts. User can now say "no" without being forced to pick a folder.

**Code changes:**
- MusicIntegrator.cs line 293: Changed prompt from `[Y] Accept [N] Choose folder [Q] Quit` to `[Y] Accept [N] Decline [Q] Quit`
- Lines 341-354: Rewrote [N] handler to simply log decline and continue (removed 60+ lines of PickFolder call logic)
- Removed PickFolder() method entirely (was only called from [N] handler)
- Decision log now records declined files with status="declined" and reason="User declined routing" for audit trail

**UX ALREADY IMPLEMENTED - Relative paths in duplicate dialogs:** Item #2 (Shorten file paths in duplicate dialogs) was already implemented. Code analysis shows: duplicatePath from AudioMirror XML is converted to libraryFilePath via DeriveLibraryPathFromMirrorPath(), then shortened to relative path (lines 112-114) before display. Duplicate dialog shows `Musivation\Akira The Don\...` not full C:\Users\... path. No additional work needed.

**Result:** Routing proposals are now faster and less intrusive. User can accept, decline, or quit. No folder picker friction. Duplicate detection shows concise relative paths for easy comparison.

---

## 2026-04-28 Session 3 - UX polish: dry-run parity, Console.Clear removal, timestamped logging

**UX FIX - Dry-run mode now shows confirmation prompts:** Previously, dry-run would auto-accept all routes without prompting user. Now both dry-run and real mode show `[Y] Accept | [N] Choose folder | [Q] Quit` prompts for every file. Dry-run only differs in action: logs what would happen without moving files. Real mode actually moves files after user approval. This ensures user can validate routing decisions before running real integration.

**UX FIX - Console.Clear() removal:** Removed all 4 `Console.Clear()` calls from routing output that were wiping routing decisions from terminal. Replaced with blank lines (Console.WriteLine()) for visual separation. Users can now scroll back and see all routing decisions that were made during dry-run and real integration.

**UX ENHANCEMENT - Timestamped log entries:** Added timestamps to all routing decision outputs using new `PrintTimestamped()` helper method. Shows exact timing of each file processing (HH:mm:ss format). Reduces duplication and improves code maintainability - previously each output line had `$"[{DateTime.Now:HH:mm:ss}]"` repeated; now uses single helper. Timestamps logged to both console and file via TeeWriter.

**Code refactoring - PrintTimestamped() helper:** Created private method to avoid duplication of timestamp boilerplate. All routing output (proposals, confirmations, moves, errors) now calls PrintTimestamped() instead of repeating `$"[{DateTime.Now:HH:mm:ss}] {message}"`. Makes code cleaner and easier to maintain. Build verified - no compilation errors.

**Result:** All TIER 0 UX prerequisites complete. Ready for user to run real integration via launch.bat with full visibility and control.

---

## 2026-04-28 Session 2 - Routing logic fixes: album subfolder, Akira The Don detection, universal confirmation gates

**PRINCIPLE APPLIED - Universal confirmation gates (user control in early stages):** Changed real integration to require user confirmation for ALL routing decisions (not just Misc/ambiguous routes). Every file now prompts: `[Y] Accept | [N] Choose folder | [Q] Quit`. Gives user full control to verify routing, propose alternatives, or stop and review. This matches the principle: "give user more control in early stages of program, later on can automate when stabilised, heavily tested, etc." Dry-run mode unaffected (still just displays proposals).

**CRITICAL BUG FIX - Akira The Don artist name detection:** Fixed ATD not routing to Musivation in dry-run. Root cause: dry-run mode doesn't modify files, so genre=Musivation tag hasn't been set yet when MusicIntegrator reads for routing. Changed detection to check primary artist name directly (Akira The Don detection now happens before genre check), so it works in both dry-run and real mode.

**CRITICAL BUG FIX - Album subfolder logic (holistic counting):** Implemented correct album subfolder routing that counts songs holistically (library + new batch combined). Rule: if 2+ songs from album exist total, route to album folder; <2 songs -> Singles/ folder. Added `CountAlbumSongs()` method that scans both library (filesystem) and NewMusic (tag reading) to determine final album song count.

**CRITICAL BUG FIX - Akira The Don routing to Musivation/People subfolder:** Implemented special routing for Akira The Don tracks: genre=Musivation triggers detection of sampled person from secondary artist. Routes to `Musivation/Akira The Don/People/{person}/` if 3+ songs exist for that person (holistic count); otherwise -> `Musivation/Akira The Don/Singles/`. Added `CountAkiraTheDonPersonSongs()` helper that counts across library People folders, Singles, and NewMusic batch. Sampled person detection via secondary artist (after first semicolon in TPE1).

**ENHANCEMENT - 3+ song rule for Akira The Don People folders:** Implemented scan-ahead-style rule for Akira The Don sampled persons, matching the Artists folder logic: 3+ songs from same person creates dedicated folder, <3 songs go to Singles. This prevents scattered single songs while ensuring organizational consistency.

**VERIFICATION - Filename regeneration:** Confirmed TagFixer already handles filename regeneration correctly. Files are renamed during tag fixing step (`newFilename = {cleanedArtists} - {cleanedTitle}.mp3`), so filenames always match final tag values by the time integration routing begins.

**Documentation updated:**
- Music-Library-Rules.md: Updated Akira The Don section with 3+ person rule and holistic counting explanation
- Both rules now reference the holistic count principle (library + batch combined, not batch-only)

**Code changes:**
- MusicIntegrator.cs: Modified `GetDestDir()` to detect Akira The Don and handle People subfolder routing with 3+ count
- Added two helper methods: `CountAlbumSongs()` and `CountAkiraTheDonPersonSongs()` for holistic counting
- Build verified - no compilation errors

**Ready for testing:** All three routing logic bugs fixed. Now ready for user to run dry-run integration to verify routing decisions match expected patterns before real integration.

---

## 2026-04-28 Session 1 - Pre-integration fix suite: logging, skip error, Unicode, and separate tag fixing (TIER 0/1)

**P0 Bug Fix - Skip error crash:** Fixed `startIndex cannot be larger than length of string` exception in MusicIntegrator when processing skipped files. Added bounds checking to all Substring() calls that compute lengths from path operations.

**P1 Comprehensive Logging:** Extended TeeWriter to support dual console + file output. Wired up timestamped log files for all modes (analysis, integrate, tagfix) in `logs/` directory. All operations now logged to file automatically - users can scan log files for errors, patterns, and auditing after integration runs. Reused existing TeeWriter instead of creating duplicate LogWriter.

**P1 Unicode Fix:** Replaced non-ASCII Unicode rightwards arrows (→) with ASCII ` ->` in TagFixer output for proper console rendering. Fixed display issues showing delta character (␦) in tag comparison output.

**TIER 0 - Separate tag fixing from integration:** Confirmed TagFixer module runs as separate pre-integration step (line 53 of MusicIntegrator.cs). Integration assumes all input files have clean tags and focuses purely on routing decisions. Separation of concerns complete.

**TIER 1 - Auto-log routing decisions to XML:** Confirmed DecisionLog class implemented and wired into MusicIntegrator. Logs all routing decisions (auto-route, manual selection, etc.) to `decisions.xml` with full track metadata, destination path, routing reason, and dryRun flag. Decision logging at lines 268/287/319/368, saved at line 402. Ready for first real integration run with full audit trail.

---

## Settled / Not Doing

These were considered but explicitly deprioritized or deemed not worth implementing:

- **AudioMirror as primary scan target** - already implemented and correct. AudioMirror XML is the source of truth for all analysis and LibChecker runs. The actual audio files are never touched during analysis. Safer, faster, version-controlled. Any future analysis tools should read from AudioMirror XML, not audio files directly.
- **Full LibChecker unit test suite** - ROI not worth it (~400+ lines, 6-12 month payback). Add tests incrementally when rules change.
- **Full integration test with fake MP3s** - too heavy, dry-run already covers this.
- **Run-state tracking** - idea to track "did I already integrate this batch?" with state.json. Deprioritized because routing decisions.xml (TIER 1) provides batch-level summary implicitly (batch timestamp, file count, outcome via entry count).
- **DECISION GATE: Python Rewrite vs .NET 8 Migration** - AudioManager is .NET Framework; could migrate to .NET 8 SDK-style (lower cost, same language, taglib#-compatible) or rewrite in Python (no build step, lightweight deps via `mutagen`). **Status:** Parked. Program works well; no immediate need to decide. Revisit only if .NET becomes a blocker or if Python advantages outweigh rewrite cost. Decision factors: token cost estimate, confirm Python libs cover TagLib# use cases, ensure file I/O scope is matched.
- **Review mode - library pruning / song-by-song decision tracking** - Exploratory feature: add a new `review` mode that walks every song one by one, shows context (tags, folder, optional play count / popularity / lyrics), and asks: keep / remove / defer. Every decision is persisted to a config file (e.g. `config/review-decisions.xml` or similar) with: song fingerprint (artist + title or file hash), decision, date, reason. Old decisions are re-surfaced periodically (e.g. after 12 months) so the review is not one-shot - tastes change, a "keep" today may be a "remove" next year. **Status:** Deprioritized. Revisit after core integration pipeline is stable and you have operational experience with large libraries.

---

## 2026-04-27 - TIER 0/TIER 3 features completed: TagFixer, LibChecker exceptions, markdown reports, README updates

Completed suite of safety and quality features spanning TIER 0 and TIER 3:

**TIER 0 - TagFixer module (pre-integration tag cleanup)**
- Created **TagFixer** command that runs before integration to clean raw NewMusic files
- Removes parenthetical phrases from Title/Album tags (e.g., `"Song (feat. Artist)"` -> `"Song"`)
- Renames files per convention: `{artists};{artists} - {title}.mp3`
- Moves featured artists to TPE1 tag (semicolon-separated)
- Sets TCMP=1 on all tracks
- Sets genre for Musivation/Motivation tracks
- Supports dry-run mode showing changes without modifying files
- Unblocks the first real integration run: user runs `tagfix` -> `integrate` -> `analyze` in sequence with zero manual tag work

**TIER 3 - Polish & documentation**
- Added `libchecker-exceptions.example.xml` template showing exception format and matching conditions
- Scripts folder structure verified: one-off Python scripts in `scripts/once-off/`; production launchers in `scripts/` root
- Dry-run coverage complete: TagFixer `--dry-run` shows tag changes, Integrator `--dry-run` shows routing decisions
- Markdown reports: ReportWriter and MusicIntegrator now output `.md` files with headers, bold, bullets, inline code
- Updated README: added folder picker mention to Integration Pipeline section and LibChecker Validations section listing all 11 checks

---

## 2026-04-27 - Constants.cs refactor and LibChecker Sources validation (TIER 1/3)

**Constants.cs refactor (TIER 3)** - Replaced all hardcoded `Path.GetFullPath(Path.Combine(BaseDirectory, "..", "..", ...))` chains with a single `FindRepoRoot()` helper that walks up the directory tree looking for sentinel files (CLAUDE.md, README.md, or .git). All paths now use: `Path.Combine(RepoRoot, "reports")` etc. Eliminates fragile depth-counting and self-heals if build output path changes. Commit: `3a3113e0`.

**LibChecker Sources OST validation (TIER 1)** - Implemented smart folder-to-album matching in `CheckSourcesFolder()`. Official soundtracks in Sources/Shows and Sources/Films are only flagged if album contains the source folder name (e.g., Peacemaker folder requires album "Peacemaker OST") but allows featured tracks to keep original album names (e.g., a-ha's "Hunting High and Low"). Eliminates 7 false positive flags and correctly distinguishes soundtrack-specific album tags from featured-track albums.

---

## 2026-04-27 - Pre-integration duplicate check and post-integration validation (TIER 0)

Implemented two safety features blocking TIER 0 to enable the first real integration run:

**Feature 1: Pre-integration duplicate check**
- Before routing each track from NewMusic, searches AudioMirror XML files for an existing track with the same primary artist AND title
- Comparison is case-insensitive and whitespace-trimmed
- If found: displays the matching library entry path and prompts user with [D]elete from NewMusic, [K]eep and continue, [Q]uit
- Dry-run mode shows what would be deleted without actually deleting
- Prevents accidentally importing songs already in the library

**Feature 2: Post-integration LibChecker auto-run**
- After integration completes successfully (not in dry-run), automatically regenerates AudioMirror and runs LibChecker on the updated library
- Catches broken integration runs immediately (zero issues expected if integration was clean)
- Reports "CLEAN" or "ISSUES FOUND" for user visibility
- Both features unblock the first real integration run by providing automated safety gates: duplicate prevention and post-integration validation

---

## 2026-04-27 - LibChecker stability: eliminated duplicate detection and false positive bugs

Two causally-linked LibChecker bugs fixed in one session, both blocking clean TIER 1 integration runs:

**Fix 1: Duplicate detection now considers all featured artists**
- Changed grouping from `{ Title, PrimaryArtist }` to `{ Title, Artists }`
- Previously: "Twista;CeeLo - Hope" and "Twista;Faith Evans - Hope" wrongly flagged as duplicates
- Now: Only flagged if title AND all artists match exactly
- Verified with full library analysis (5489 tracks) - no false positives

**Fix 2: Eliminated 'feat.' and 'ft.' false positives in titles/filenames**
- Changed unwanted-string detection from simple `Contains` to regex for abbreviations
- Previously flagged mid-word matches: "Identity Theft" (ft.), "NEVER LEFT" (ft.), "NO TEARS LEFT" (ft.)
- Now requires whitespace/punctuation before abbreviation: `(?:^|[\s\-\(\)\[\]\/,])`
- Legitimate featured artists like "(feat. Artist)" or " ft. Artist" still correctly flagged
- Verified: full library analysis, 0 false positives for "feat."/"ft."

Both fixes are in the same file (LibChecker.cs) and address the same outcome: enabling reliable TIER 1 integration runs. Committed separately but documented together as a subsystem stabilization.

---

## Previous entry (2026-04-27) - Fixed LibChecker duplicate detection to consider all featured artists

(Merged into the combined entry above.)

LibChecker's duplicate detection was grouping tracks by `{ Title, PrimaryArtist }` only, incorrectly flagging different songs with the same title but different featured artists as duplicates. Example: "Twista;CeeLo - Hope" and "Twista;Faith Evans - Hope" were wrongly grouped together despite having different collaborators.

Fixed by changing the grouping key from `PrimaryArtist` (first artist only) to the full `Artists` field (all collaborators). Now duplicates are only flagged when both title AND all featured artists match exactly. Verified with full library analysis (5489 tracks) - no false positives, and program correctly identifies true duplicates (of which there are currently zero).

This unblocks TIER 1 integration runs, which depend on reliable duplicate detection to prevent importing songs already in the library.

---

## 2026-04-27 - Fixed build script for VS2026 (MSBuild path update)

Visual Studio 2022 Community updated to the 2026 version (VS 18). The old MSBuild path `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe` was no longer valid. Updated `scripts/build.bat` to use the new path: `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`. Build now succeeds with MSBuild 18.5.4. Verified by running build script end-to-end - AudioManager.exe compiles cleanly.

---

## 2026-04-26 - Pre-integration gate: verify AudioMirror fresh and LibChecker clean

Implemented mandatory safety gate before integration runs. When user invokes `integrate` mode:
1. Regenerates AudioMirror XMLs to ensure current library state is captured
2. Checks if XMLs changed (ignores LastRunInfo.txt timestamp-only updates) - if XMLs changed, mirror is stale
3. Runs LibChecker on the fresh data - if any issues found, must be fixed first
4. Only proceeds to integration if BOTH checks pass: fresh mirror AND clean library

Error messages guide user: "AudioMirror is out of sync with the library" or "LibChecker found issues in the library. Fix library issues before adding new songs."

Prevents corruption risk by ensuring new songs are integrated only when library state is known-good. Tested with real library (5491 tracks) - gate correctly rejects integration when LibChecker finds issues, allows proceed when both conditions met.

---

## 2026-04-25 - Phase 0 complete: program verified runnable end-to-end

All Phase 0 blockers resolved. MSBuild compiles clean. `integrate --dry-run` handles empty inbox correctly. `analysis` runs end-to-end: mirror regenerated (5491 tracks), metadata parsed, stats generated, LibChecker ran, report saved, AudioMirror commit correctly blocked due to LibChecker hits.

- `launch.bat` platform flag fixed (`x86` -> `Any CPU`) - done 2026-04-10
- `AudioMirrorCommitter.cs` registered in csproj - done 2026-04-10
- CRITICAL csproj file-registration note added to CLAUDE.md Build and Run section - done 2026-04-10
- Full end-to-end smoke test (MSBuild + all modes) verified by Claude - done 2026-04-25

---

## 2026-04-10 - Constants.MirrorRepoPath made absolute (cwd-independent)

`Constants.MirrorRepoPath` was built from the relative `ProjectPath = "..\\..\\..\\"` literal, which .NET resolves against the **current working directory**, not the exe location. When launched via `scripts/launch.bat` (cwd = `scripts\`), the relative path walked up too far and resolved to `C:\AudioMirror\` - producing `Could not find path 'C:\AudioMirror\LastRunInfo.txt'` in `AgeChecker..ctor` before anything else could run. Fixed by rebuilding `MirrorRepoPath` with `Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "AudioMirror"))` - same pattern already used by `ReportsPath`, `LibCheckerExceptionsPath`, and `LogsPath`. Also tidied `MirrorFolderPath` to use `Path.Combine` (removes a stray double-backslash). Callers that already wrapped `MirrorRepoPath` in `Path.GetFullPath(Path.Combine(...))` continue to work (idempotent). Verified by running `AudioManager.exe analysis` from a `scripts\` cwd - mirror path now resolves to `C:\Users\David\GitHubRepos\AudioMirror\AUDIO_MIRROR` and the full analysis pipeline runs clean.

---

## 2026-04-10 - Build fixes: launch.bat platform + missing csproj Compile entry

Two build failures surfaced when trying to run LibChecker for the first time since the integration pipeline work:

1. **`scripts/launch.bat` line 19** passed `-p:Platform=x86` to MSBuild, but the solution only defines `Debug|Any CPU` and `Release|Any CPU`. Error: `MSB4126: The specified solution configuration "Release|x86" is invalid`. Fixed to `-p:Platform="Any CPU"`. CLAUDE.md line 52 had the same bad example and was also corrected, so future sessions don't keep copying it.

2. **`AudioMirrorCommitter.cs` was never registered in `AudioManager.csproj`.** The file was created in an earlier session but the old-style .NET Framework csproj requires an explicit `<Compile Include="..." />` entry per file - new files are NOT auto-included. Error: `CS0103: The name 'AudioMirrorCommitter' does not exist in the current context`. Added the Compile entry. Added a CRITICAL guard rule to CLAUDE.md "Build and Run" section so this can't silently recur - any new .cs file must be registered in the csproj.

---

## 2026-04-09 - LibChecker: album subfolder rule + inverse genre check

Two new checks added based on Music-Library-Rules.md gap analysis:

- `CheckAlbumSubfolderRule()`: flags Artists/ tracks violating the album subfolder rule.
  - 2+ songs from same album in `Singles/` instead of an album subfolder.
  - Only 1 song from an album in an album-named subfolder instead of `Singles/`.
- `CheckGenreVsFolder(folder, genre)`: flags tracks with Musivation/Motivation genre that are
  outside their expected folder (inverse of the existing `CheckMainFolderForGenre` check).
- `Constants.SinglesDir = "Singles"` added.

---

## 2026-04-09 - LibChecker exceptions moved to config file

`IsExceptionToRules()` hardcoded whitelists extracted to `config/libchecker-exceptions.xml`. LibChecker now loads exceptions at startup via `LoadExceptions()`. Add new exceptions without recompiling - edit the XML and run. Existing exceptions: KRS-One, Original Rappers, Going To Be Alright, Medicine Man, Edition albums, Soundtrack 2 My Life, Agatha All Along, Eric Thomas bonus interview.

---

## 2026-04-09 - AudioMirror auto-commit, scan-ahead routing, LibChecker IsClean

- `AudioMirrorCommitter.TryCommit()`: after every analysis run, if LibChecker clean and AUDIO_MIRROR changed, auto-commits with "MMM d Update" message and pushes. Skips if issues found or nothing changed.
- `LibChecker.IsClean`: new property. `grandTotalHits` accumulates across all check methods via `PrintTotalHits()`. Prints "LibChecker: Clean" when zero hits. Program.cs detects this from captured output.
- `RunScanAhead()`: pre-scans batch MP3 tags + AudioMirror Misc XMLs. Artists hitting 3+ threshold get routed to `Artists/{artist}/` instead of Misc. Preview printed before per-file loop. Existing Misc songs for those artists flagged for manual migration (not auto-moved - risky).
- `GetDestDir()`: scan-ahead HashSet passed in. Routes scan-ahead artists to Artists/ with "[new via scan-ahead]" reason note.

---

## 2026-04-09 - Integration pipeline: pre-process tags, log, routing fix

- `PreProcessTags()` runs on every incoming file before routing: sets TCMP=True on all tracks, sets Genre=Musivation for Akira The Don. Works in dry-run mode (prints what would change without writing).
- `SaveLog()` writes `logs/integration-YYYYMMDD[-dryrun].txt` after every run. Per-file: filename, artists, title, album, destination, tag changes, status. Summary line with totals.
- `GetDestDir()`: fixed routing bug - when artist folder exists but no distinct album, routes to `Singles/` instead of artist root (prevents loose files that LibChecker would flag).
- TagLib resource leak fixed: `TagLib.File.Create()` now wrapped in `using` block.
- `SourcesDir = "Sources"` added to Constants (was the only main folder without one).
- `CheckSourcesFolder()` added to LibChecker: flags Sources/Films and Sources/Shows tracks where Album does not contain "OST".
- Analyser library size: fixed to count only `.mp3` files (previously included .ini, .lnk, etc.).
- `LogsPath` added to Constants (gitignored `logs/` folder).

---

## 2026-04-09 - Batch launcher, CLI args, dry-run mode

- `scripts/launch.bat` - menu-driven launcher, auto-builds via MSBuild, 4 modes (analysis / force-regen / dry-run / real integrate), `cmd /k` to keep window open.
- `Program.cs` - CLI args support: `AudioManager.exe analysis [--force-regen]` or `AudioManager.exe integrate [--dry-run]`. Interactive menu still works when no args passed.
- `MusicIntegrator` - `--dry-run` flag: prints all planned moves without executing any file operations. Shows `[DRY RUN]` prefix. Summary says "Would move" instead of "Moved".

---

## 2026-04-08 - Docs & Knowledge tasks complete

- **Batch A date fixed** - `NewMusic-Integration-Plan-20260407.md` renamed to `20260308`; AudioMirror commit `b8e15b1` confirmed the integration was March 8 2026, not April 7.
- **Music-Library-Rules.md expanded** - Added Sources folder rules (Films, Shows, Anime), full workflow (Stages 1-3), Compilations folder, MP3 conversion, "no album -> use title" rule, Miscellaneous review guidance. Sources folder was completely missing before.
- **Word doc retired as source of truth** - All content from `Audio Folder Organisation Usage Process.docx` extracted into `Music-Library-Rules.md`.
- **Integration plan review** - Both Batch A (20260308) and Batch B (20260407b) reviewed; no additional routing rules found that weren't already in `Music-Library-Rules.md` after the expansion.

---

## 2026-04-08 - Architecture decisions settled

**Integrator stays in AudioManager** - shares Constants, LibChecker, and tag models with the rest of AudioManager. Splitting would require duplicating or packaging shared code with no real gain at this scale. Logical separation (each component has its own entry point) is sufficient.

**Audio reports stay in AudioManager** - AudioMirror is a data repo; generating/storing analysis reports there would blur its purpose. AudioManager owns the tools and the outputs.

**LibChecker output in audio report** - "Checking library..." process lines do not belong in a stats report. Rule: if LibChecker clean, show a single `LibChecker: Clean` line in the report. If issues exist, prominently flag them in the report AND block the AudioMirror commit - fix issues first, then commit both the report and AudioMirror changes.

---

## 2026-04-08 - LibChecker: version/edition suffix detection

Added `"version"` and `"explicit"` to `Constants.UnwantedInfo`. LibChecker now flags titles containing `(Explicit Version)`, `(Album Version)` etc. `(Radio Edit)` and `(Deluxe Edition)` were already caught by the existing `"edit"` entry.

---

## Constants.cs consolidation (already done)

`MiscDir`, `ArtistsDir`, `MusivDir`, `MotivDir` were already defined in `Constants.cs`. Both `LibChecker` and `MusicIntegrator` already read from `Constants` - no duplication existed.

---

## LibChecker owns validation (already done)

`MusicIntegrator`'s only "validation" is a routing precondition (skip files missing artist/title - can't determine destination without them). This is not duplicated LibChecker logic. The boundary was already clean.

---

## ReportWriter - plain static class (already done)

`ReportWriter` is already `internal static class`, not inheriting from Doer. Idea retired.

---

## Analyser stats (already implemented)

Total playback hours, average and median song length, total library size (GB), average file size (MB), track age stats (average, median, oldest, newest), year and decade frequency distribution. All in `Analyser.cs`.

---

## 2026-04-07 - Fix git folder casing

Git tracked folders in old uppercase names (`PROJECT/`, `REPORTS/`, `Docs/`) while they were lowercase on disk. Fixed via two-step `git mv` per folder (required because Windows filesystem is case-insensitive and ignores direct renames). Also updated `REPORTS` path string in `Constants.cs` and comments in `ReportWriter.cs` to match.

---

## 2026-06-27 - Fix three routing bugs found in integration dry run

**Bug 1: Scan-ahead album key mismatch (broke album subfolder routing)**
`RunScanAhead` stored raw album tags in `batchAlbumCounts` (e.g. "The Blueprint (Explicit Version)"). `CountAlbumSongs` queried by normalized album name (e.g. "The Blueprint"). Key mismatch caused 0 batch songs found for every album with a suffix, routing all such songs to Singles instead of an album subfolder. Fix: normalize album in `RunScanAhead` using `TagFixer.StripAlbumSuffixes(TagFixer.RemoveParentheticals(album))` before storing. Root cause: two passes over the same data applied different transformations. Invariant: scan-ahead and routing must use the same normalization.

**Bug 2: Self-titled album exclusion prevented album folder routing**
`GetDestDir` had `!track.Album.Equals(primaryArtist2)` condition that treated albums with the same name as the artist as "no distinct album", routing all their songs to Singles. This blocked a 6-song Paperboys EP (album "Paperboys") from getting an album folder. Fix: removed the condition. Album count alone determines routing. Self-titled albums are valid albums.

**Bug 3: Artist casing normalization destroyed abbreviations and initials**
`ExtractAndFixArtists` applied `a.ToLower()` before `ToTitleCase()` unconditionally. This turned "PJ Simas" -> "pj simas" -> "Pj Simas" and "XV" -> "xv" -> "Xv". Fix: skip `.ToLower()` for mixed-case names (has both upper and lower letters); add "XV" and "PJ Simas" to `artist-name-overrides.xml`. Three tests added covering these cases.

Also fixed: display slash inconsistency (Path showed backslashes, Route showed forward slashes). Path now uses forward slashes for consistency.

---

## 2026-06-27 - Artist-alias duplicate detection + routing output UX

**Artist-alias duplicate detection (TIER 1):**
Added `config/artist-aliases.xml` (`Kanye West -> Ye`). `BuildMirrorIndex` now expands alias keys in both directions when building the mirror index: a library entry stored under "Kanye West" is also indexed under "ye", so when a new batch file arrives with artist "Ye", the duplicate check catches it (and vice versa). Internal helpers `LoadArtistAliases` and `GetAliasExpandedKeys` are tested. Degrades gracefully if config missing. 6 new tests.

**Routing output UX (TIER 2):**
- Album now shown on every routing header line: `[AUTO] Artist - Title [Album]`. No extra line; brackets keep it compact.
- `[AUTO - new folder]` label removed. New-folder routes show `[AUTO]` on the header and ` *` suffix on the Route line (e.g. `Chiddy Bang / Breakfast *`). Scan-ahead summary at the top already lists which artists get new folders; the per-block label was redundant.

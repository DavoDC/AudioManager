"""Integration tab - GUI-native staged workflow (NOT a terminal).

Stage 1  Scan       integrate --dry-run --no-input --json-output -> routing JSON
Stage 2  Review     one card per proposed track: art, destination, reason,
                    tag-change chips, badges, and a single accept/decline
                    toggle (plus an explicit keyboard Triage mode)
Stage 3  Confirm    summary bar + single primary action
Stage 4  Execute    real integrate --no-input with structured per-track
                    progress parsed from the exe's live output

Per-track selective execution is real: when tracks are declined, the GUI
writes the accepted set to gui/.cache/accepted-manifest.json and runs
`integrate --manifest <path> --no-input` - the exe moves only the accepted
files and leaves declined ones untouched in NewMusic (the GUI itself never
moves or deletes NewMusic files; the exe owns all file operations).

Simulate mode: "Simulate (sample data)" on the scan stage skips the real exe
entirely and loads synthetic entries covering every review-card state, then
"Integrate" on the confirm stage runs a synthetic execute (run_execute_simulated)
that feeds the exe's real per-line output formats through the same status/
summary logic as a real run (_finish_execute) - so the full stage 1-4 flow can
be exercised and screenshotted without ever invoking AudioManager.exe or
touching a real NewMusic file. `IntegrationState.simulated` gates this and is
surfaced in the UI so a simulated run can never be mistaken for a real one.

One sample entry is deliberately given status "error" (unresolved: missing
required tag) to exercise the review card's error-badge rendering - but per
IntegrationState.accepted, an error-status entry can never be accepted, so it
never reaches run_execute_simulated's targets and Simulate mode can never show
a failed integration run. Real mid-batch failure output (`INTEGRATION FAILED`,
"Error processing file: ...") is only ever seen on a real run, parsed by
_finish_execute/_failed_filename_from_output below.
"""
from __future__ import annotations

import asyncio
import html
import json
from datetime import datetime

from nicegui import ui

from gui import config, routing
from gui.art import get_thumbnail, initials, placeholder_style
from gui.components.error_modal import show_error_modal
from gui.runner import RunResult, runner

SIMULATE_STEP_DELAY = 0.15


class IntegrationState:
    def __init__(self):
        self.stage = 1                      # 1 scan, 2 review, 3/4 execute
        self.entries: list[dict] = []
        self.decisions: dict[str, bool] = {}   # filename -> accepted
        self.dup_resolutions: dict[str, str] = {}  # filename -> "D"/"L"/"K" (explicit override only)
        self.filter = "all"                 # all | conflicts | newfolders | libdupes
        self.cursor = 0                     # index into filtered() for keyboard nav (A/D/J/K)
        self.triage = False                 # keyboard Triage mode: cursor visible, A/D armed
        self.scan_lines: list[str] = []
        self.exec_lines: list[str] = []
        self.exec_status: dict[str, str] = {}  # filename -> queued/moving/done/failed/notrun
        self.exec_targets: list[dict] = []     # the accepted entries being executed
        self.exec_done = False
        self.exec_summary = ""
        self.exec_ok: bool | None = None    # None: no run finished yet; True/False: last run's outcome
        self.simulated = False              # True: sample data, no real exe/file touched
        self.summary: dict = routing.empty_summary()  # batch scan-ahead context (routes, Misc migrations, compilations)
        self.projected_libchecker: dict | None = None  # dry run's own safety verdict
        self.confidence_report: dict | None = None  # real run's post-run integrity check
        self.refresh = lambda: None

    @property
    def accepted(self) -> list[dict]:
        return [e for e in self.entries
                if e.get("status", "").lower() != "error" and self.decisions.get(e["filename"], True)]

    @property
    def declined(self) -> list[dict]:
        return [e for e in self.entries if not self.decisions.get(e["filename"], True)]

    def filtered(self) -> list[dict]:
        if self.filter == "conflicts":
            entries = [e for e in self.entries
                       if e["inBatchDuplicate"] or e["status"].lower() not in ("", "ok", "clean", "moved", "route")]
        elif self.filter == "newfolders":
            entries = [e for e in self.entries if e["isNewFolder"]]
        elif self.filter == "libdupes":
            entries = [e for e in self.entries if e["libraryDuplicate"]]
        elif self.filter == "compilations":
            entries = [e for e in self.entries if e.get("compilationAlbum")]
        else:
            entries = self.entries
        return sort_by_destination(entries)

    def dup_resolution(self, e: dict) -> str:
        """The resolved D/L/K decision for a library-duplicate entry: an
        explicit review-stage override if one was made, else the exe's own
        recommendation. This is deliberately the SAME lookup used both for
        "left untouched" here and for the exe-side fallback in
        MusicIntegrator.GetDupResolution (a manifest with no dupResolution,
        or one the exe can't recognise, falls back to auto-accept-
        recommendation) - one default, not two that could drift apart."""
        override = self.dup_resolutions.get(e["filename"])
        if override in ("D", "L", "K"):
            return override
        key = e.get("dupRecommendationKey") or "K"
        return key if key in ("D", "L", "K") else "K"


def sort_by_destination(entries: list[dict]) -> list[dict]:
    """Groups same-album files adjacent by sorting on destination path -
    client-side mirror of the CLI's own sort (`MusicIntegrator.cs` ~188-192).
    The exe's routing JSON is raw scan order; nothing on the C# side needs to
    change for the GUI to present the same grouping. Filename is the tie-break
    so entries sharing one destination keep a stable, readable sub-order."""
    return sorted(entries, key=lambda e: (e.get("destination") or "", e.get("filename") or ""))


def _review_key_action(key_name: str) -> str | None:
    """Maps a raw keyboard key name to a review-stage action - pure and
    case-insensitive so it's unit-testable without a real NiceGUI KeyboardKey.
    'a'/'d' accept/decline the card under the cursor; 'j'/ArrowDown and
    'k'/ArrowUp move the cursor; 't' toggles Triage mode and Escape leaves it.
    Anything else is not a review shortcut.

    Mapping only - it says nothing about whether the action is currently
    allowed. Which of these keys are armed depends on Triage mode, and that
    gate lives in _on_review_key() alone so there is one place to read it."""
    name = (key_name or "").lower()
    if name == "a":
        return "accept"
    if name == "d":
        return "decline"
    if name in ("j", "arrowdown"):
        return "next"
    if name in ("k", "arrowup"):
        return "prev"
    if name == "t":
        return "triage"
    if name in ("escape", "esc"):
        return "exit"
    return None


#: Actions that change a decision. Armed only while Triage mode is on - see
#: _on_review_key(). Navigation is deliberately NOT in here: moving a cursor
#: is harmless, so J/K/arrows stay live and switch Triage mode on themselves.
TRIAGE_ONLY_ACTIONS = ("accept", "decline")


S = IntegrationState()


def build() -> None:
    with ui.element("header").classes("page"):
        ui.html("<h1>Integration</h1>")
        ui.html('<div class="meta">Staged review of NewMusic - scan, review routing, confirm, integrate</div>')

    @ui.refreshable
    def content():
        if S.simulated:
            ui.html('<div class="simulate-banner">SIMULATED - sample data, no real exe call, '
                    "no NewMusic file read or moved.</div>")
        stepper()
        if S.stage == 1:
            stage_scan()
        elif S.stage == 2:
            stage_review()
        else:
            stage_execute()
        advanced_log()

    S.refresh = content.refresh
    content()


def stepper() -> None:
    """Three steps, because there are three screens. Confirm is a modal over the
    review stage, never a screen of its own, so it was a step that could never
    render as active - the stepper jumped from 2-active straight to 2+3-done.
    S.stage 3 and 4 are both the execute screen, so they collapse onto step 3."""
    steps = [("Scan NewMusic", 1), ("Review routing", 2), ("Integrate", 3)]
    current = min(S.stage, 3)
    html = ['<div class="stepper">']
    for label, n in steps:
        done = current > n or (current == n == 3 and S.exec_done)
        cls = "done" if done else ("active" if current == n else "")
        mark = "&#10003;" if done else str(n)
        html.append(f'<div class="step {cls}"><span class="n">{mark}</span> {label}</div>')
    html.append("</div>")
    ui.html("".join(html)).classes("w-full")


# ------------------------------------------------------------ stage 1 scan


def stage_scan() -> None:
    with ui.element("div").classes("panel w-full").style("padding:34px;text-align:center;"):
        ui.html('<div style="font-size:15px;font-weight:600;color:var(--text);margin-bottom:6px;">'
                "Scan the NewMusic inbox</div>")
        ui.html('<div class="note" style="margin:0 0 16px;">Runs the exe&#39;s dry run '
                "(<span style=\"font-family:var(--font-mono)\">integrate --dry-run --json-output</span>) "
                "- previews every routing decision without touching a single file.</div>")
        with ui.row().classes("w-full justify-center").style("gap:10px;"):
            if runner.busy:
                ui.html('<div class="note" style="margin:0;"><span class="spin"></span>'
                        f"{runner.current_action} running&hellip;</div>")
                ui.button("Cancel", icon="cancel", on_click=lambda: runner.cancel()) \
                    .props("outline color=negative dense")
            else:
                ui.button("Scan NewMusic", icon="travel_explore", on_click=lambda: asyncio.create_task(run_scan())) \
                    .props("unelevated color=primary")
                ui.button("Simulate (sample data)", icon="science", on_click=run_simulate) \
                    .props("outline color=grey dense")
        ui.html('<div class="note" style="margin:8px 0 0;">Simulate loads synthetic sample entries '
                "and a synthetic execute run - no real exe call, no NewMusic file is read or moved. "
                "Use it to review the workflow's design without needing anything in the inbox.</div>")


def _sample_entry(filename: str, artist: str, title: str, album: str, **overrides) -> dict:
    e = {
        "filename": filename, "artist": artist, "title": title, "album": album,
        "destination": "", "reason": "", "isNewFolder": False,
        "status": "ok", "inBatchDuplicate": False, "compilationAlbum": False, "tagChanges": [],
        "libraryDuplicate": False, "dupLibraryPath": "", "dupLibraryTrack": "",
        "dupLibraryAlbum": "", "dupNewAlbum": "", "dupRecommendationKey": "",
        "dupRecommendation": "", "dupReason": "",
    }
    e.update(overrides)
    return e


def _sample_entries() -> list[dict]:
    """Synthetic sample data for Simulate mode - one entry per review-card
    state (clean route, new folder, in-batch duplicate, library duplicate,
    tag changes, error/unresolved) so a single click exercises every badge
    and filter.

    In-batch duplicate and library duplicate are deliberately kept on two
    separate entries here (docs/Development/IDEAS.md "Duplicate-resolution
    UI") - they are unrelated concepts (same artist+title twice in this scan
    batch vs. already present somewhere in the library) and mixing them onto
    one sample would make the two badges/filters look coupled when they
    aren't."""
    return [
        _sample_entry("Bring Me The Horizon - Doomed.mp3", "Bring Me The Horizon", "Doomed",
                       "Post Human: Survival Horror",
                       destination="Artists/Bring Me The Horizon/Post Human- Survival Horror"),
        _sample_entry("Polo G;Lil Wayne - Gang With Me.mp3", "Polo G;Lil Wayne", "Gang With Me", "The Goat",
                       destination="Artists/Polo G/Singles", isNewFolder=True,
                       reason="New artist - scan-ahead below 3-song threshold"),
        _sample_entry("Ariana Grande - Positions (Remix).mp3", "Ariana Grande", "Positions (Remix)",
                       "Positions", destination="Artists/Ariana Grande/Positions", inBatchDuplicate=True,
                       reason="Same artist/title appears twice in this NewMusic batch"),
        _sample_entry("Kendrick Lamar - HUMBLE.mp3", "Kendrick Lamar", "HUMBLE.", "DAMN. (Deluxe)",
                       destination="Artists/Kendrick Lamar/DAMN.", libraryDuplicate=True,
                       reason="Already exists in the library",
                       dupLibraryPath="Artists\\Kendrick Lamar\\DAMN.\\Kendrick Lamar - HUMBLE.mp3",
                       dupLibraryTrack="HUMBLE.", dupLibraryAlbum="DAMN.",
                       dupNewAlbum="DAMN. (Deluxe)", dupRecommendationKey="L",
                       dupRecommendation="Delete library copy",
                       dupReason="New file album 'DAMN. (Deluxe)' is a deluxe/expanded version of "
                                 "library album 'DAMN.' - deluxe preferred"),
        _sample_entry("Dua Lipa - Levitating.mp3", "Dua Lipa", "Levitating", "Future Nostalgia",
                       destination="Artists/Dua Lipa/Future Nostalgia",
                       tagChanges=["Title: 'levitating' -> 'Levitating'", "Album: added"]),
        _sample_entry("Corrupt Metadata Track.mp3", "", "", "",
                       destination="", status="error", reason="Missing required tag: artist"),
        _sample_entry("Fred again.. - Delilah.mp3", "Fred again..", "Delilah (pull me out of this)",
                       "Actual Life 3", destination="Artists/Fred again../Actual Life 3"),
        # Compilation album: 3+ distinct primary artists share this album in-batch, so it
        # routes to Compilations/ rather than an artist folder. Exercises the compilation
        # badge and filter, and the summary's compilationAlbums line.
        _sample_entry("Various - Song For Denise.mp3", "Piano Fantasia", "Song For Denise",
                       "Now That's What I Call Music 42",
                       destination="Compilations/Now That's What I Call Music 42",
                       compilationAlbum=True,
                       reason="Compilation album (3+ distinct artists in batch); no artist "
                              "folder -> Compilations/Now That's What I Call Music 42/"),
    ]


def run_simulate() -> None:
    if runner.busy:
        ui.notify("Another operation is already running", type="warning")
        return
    entries = _sample_entries()
    S.entries = entries
    S.decisions = {e["filename"]: True for e in entries}
    S.exec_status = {}
    S.exec_done = False
    S.exec_ok = None
    S.cursor = 0
    S.triage = False
    S.scan_lines = ["[SIMULATE] Sample data only - nothing in NewMusic was scanned or touched."]
    S.simulated = True
    # Batch scan-ahead context the real exe emits in the routing JSON's summary block.
    S.summary = {
        "routes": {"Artists": 6, "Compilations": 1},
        "miscAutoMigrations": [{"artist": "Polo G", "count": 2}],
        "miscAutoMigrationTotal": 2,
        "compilationAlbums": ["Now That's What I Call Music 42"],
    }
    S.projected_libchecker = {
        "summary": "Projected library: 412 current, -0 removals, +6 additions = 418 projected",
        "clean": True, "skipped": False, "total_hits": 0,
    }
    S.stage = 2
    S.refresh()


async def run_scan() -> None:
    if runner.busy:
        ui.notify("Another operation is already running", type="warning")
        return
    S.scan_lines = []
    S.simulated = False
    S.refresh()
    result = await runner.run(
        ["integrate", "--dry-run", "--json-output"],
        action="Scan (dry run)",
        on_line=S.scan_lines.append,
        timeout=config.TIMEOUT_DRY_RUN,
    )
    if not result.ok:
        S.refresh()
        if not result.cancelled:
            show_error_modal("Scan (dry run)", result, retry=run_scan)
        return
    path = routing.routing_path_from_output(result.lines)
    entries = []
    summary = routing.empty_summary()
    if path:
        try:
            entries, summary = routing.parse_routing_document(path)
        except (OSError, ValueError) as e:
            ui.notify(f"Could not parse routing JSON: {e}", type="negative")
    S.entries = entries
    S.summary = summary
    S.decisions = {e["filename"]: True for e in entries}
    S.exec_status = {}
    S.exec_done = False
    S.exec_ok = None
    S.cursor = 0
    S.triage = False
    S.projected_libchecker = routing.parse_projected_libchecker(result.lines)
    S.stage = 2
    S.refresh()


# --------------------------------------------------------- stage 2 review


def stage_review() -> None:
    if not S.entries:
        with ui.element("div").classes("panel w-full").style("padding:34px;text-align:center;"):
            ui.html('<div style="font-size:15px;font-weight:600;color:var(--text);margin-bottom:6px;">'
                    "NewMusic is empty</div>")
            ui.html('<div class="note" style="margin:0 0 14px;">The dry run found no files to route. '
                    "Drop new MP3s into the NewMusic folder and re-scan.</div>")
            ui.button("Re-scan", icon="refresh", on_click=lambda: asyncio.create_task(run_scan())) \
                .props("outline color=primary dense")
        return

    n_new = sum(1 for e in S.entries if e["isNewFolder"])
    n_dupe = sum(1 for e in S.entries
                 if e["inBatchDuplicate"] or e["status"].lower() not in ("", "ok", "clean", "moved", "route"))
    n_libdupe = sum(1 for e in S.entries if e["libraryDuplicate"])
    n_comp = sum(1 for e in S.entries if e.get("compilationAlbum"))

    projected_libchecker_strip()

    with ui.row().classes("w-full items-center justify-between").style("margin-bottom:12px;flex-wrap:wrap;gap:10px;"):
        with ui.row().style("gap:8px;flex-wrap:wrap;"):
            chips = [("all", f"All ({len(S.entries)})"),
                     ("newfolders", f"New folders ({n_new})"),
                     ("conflicts", f"Duplicates / conflicts ({n_dupe})"),
                     ("libdupes", f"Library duplicates ({n_libdupe})")]
            # Only offered when the batch actually has one - an always-present
            # "Compilations (0)" chip is the same anti-signal as a badge that
            # means "nothing here".
            if n_comp:
                chips.append(("compilations", f"Compilation albums ({n_comp})"))
            for key, label in chips:
                cls = "chip active" if S.filter == key else "chip"
                chip = ui.html(f'<button type="button" class="{cls}">{label}</button>')
                chip.on("click", lambda _, k=key: _set_filter(k))
        with ui.row().style("gap:8px;"):
            ui.button("Accept all", icon="done_all", on_click=lambda: _bulk(True)) \
                .props("outline dense color=positive size=sm")
            ui.button("Decline all", icon="clear_all", on_click=lambda: _bulk(False)) \
                .props("outline dense color=negative size=sm")
            ui.button("Re-scan", icon="refresh", on_click=lambda: asyncio.create_task(run_scan())) \
                .props("outline dense color=grey size=sm")
            ui.button("Triage mode", icon="keyboard", on_click=lambda: _set_triage(not S.triage)) \
                .props(f'dense size=sm color={"primary" if S.triage else "grey"} '
                       f'{"unelevated" if S.triage else "outline"}')

    entries_view = S.filtered()
    if entries_view:
        S.cursor = max(0, min(S.cursor, len(entries_view) - 1))
    ui.keyboard(on_key=_on_review_key)
    triage_bar(len(entries_view))
    for idx, e in enumerate(entries_view):
        review_card(e, idx)
    if S.triage:
        _scroll_cursor_into_view()

    batch_summary_strip()
    confirm_bar()


def projected_libchecker_strip() -> None:
    """The dry run already answers "will my library still be clean after
    this" (RunProjectedLibChecker in MusicIntegrator.cs) - surface that
    verdict here instead of leaving it buried in the collapsed raw-output
    panel, which is where it lives today."""
    v = S.projected_libchecker
    if v is None:
        return
    if v["skipped"]:
        ui.html(f'<div class="libchecker-strip skip">Projected LibChecker: not run - '
                f'{_esc(v["summary"])}</div>')
    elif v["clean"]:
        ui.html(f'<div class="libchecker-strip clean">&#10003; Projected LibChecker: clean - '
                f'{_esc(v["summary"])}</div>')
    else:
        hits = f'{v["total_hits"]} issue(s)' if v["total_hits"] else "issues found"
        ui.html(f'<div class="libchecker-strip dirty">&#9888; Projected LibChecker: {hits} - '
                f'this run would leave the library dirty. See Advanced / raw output for detail.</div>')


def confidence_report_strip() -> None:
    """The exe's post-run CONFIDENCE REPORT (count check + destination sanity
    check re-reading every moved file with TagLib) is the single strongest
    guarantee a claimed-successful run actually succeeded - it used to be
    reachable only via "Advanced / raw output (debug)", which undersold it."""
    v = S.confidence_report
    if v is None:
        return
    if not v["count_ok"] or not v["sanity_ok"] or v["error_count"]:
        parts = []
        if not v["count_ok"]:
            parts.append("file count mismatch")
        if not v["sanity_ok"]:
            parts.append("a moved file failed the sanity check")
        if v["error_count"]:
            parts.append(f'{v["error_count"]} error(s)')
        ui.html(f'<div class="libchecker-strip dirty">&#9888; Confidence check failed - '
                f'{_esc(", ".join(parts))}. See Advanced / raw output for detail.</div>')
    elif v["sanity_summary"]:
        ui.html(f'<div class="libchecker-strip clean">&#10003; Confidence check - '
                f'{_esc(v["sanity_summary"])}</div>')
    elif v["count_line"]:
        ui.html(f'<div class="libchecker-strip clean">&#10003; Confidence check - '
                f'{_esc(v["count_line"])}</div>')


def _set_filter(k: str) -> None:
    S.filter = k
    S.cursor = 0
    S.refresh()


def _on_review_key(e) -> None:
    """Global keyboard handler, mounted only while stage_review() is on
    screen (stage 2) - never active during scan/confirm/execute.

    Triage mode is the gate. A (accept) and D (decline) only act while it is
    on, because a bare letter key that silently flips a decision on a card
    the user may not even be looking at is the mis-key hazard this redesign
    exists to remove. Navigation is exempt and self-arming: J/ArrowDown and
    K/ArrowUp move the cursor AND switch Triage mode on, so the keyboard path
    is still discovered by pressing an arrow key rather than by reading docs -
    it just announces itself (visible cursor + key legend) before any key can
    change a decision. T toggles the mode, Escape leaves it.

    The duplicate-resolution radio control stays mouse-only, deliberately.
    When it is made keyboard-driven it must become a SECOND AXIS on this same
    cursor (e.g. 1/2/3 or Left/Right setting D/L/K on the card under the
    cursor), never a nested focus cursor that J/K have to step through - one
    cursor, one Triage mode. See docs/Development/IDEAS.md."""
    if not e.action.keydown or e.action.repeat:
        return
    if S.stage != 2 or not S.entries:
        return
    action = _review_key_action(e.key.name)
    if action is None:
        return
    if action == "triage":
        _set_triage(not S.triage)
        return
    if action == "exit":
        if S.triage:
            _set_triage(False)
        return
    if action in TRIAGE_ONLY_ACTIONS and not S.triage:
        return
    entries = S.filtered()
    if not entries:
        return
    S.cursor = max(0, min(S.cursor, len(entries) - 1))
    S.triage = True
    if action == "next":
        S.cursor = min(S.cursor + 1, len(entries) - 1)
        S.refresh()
    elif action == "prev":
        S.cursor = max(S.cursor - 1, 0)
        S.refresh()
    elif action == "accept":
        _decide(entries[S.cursor]["filename"], True)
    elif action == "decline":
        _decide(entries[S.cursor]["filename"], False)


def _set_triage(on: bool) -> None:
    """Entering Triage mode always starts the cursor from the top of the
    current filtered view - a cursor left over from a previous session on a
    different filter would arm A/D against a card that isn't where the user
    is looking."""
    S.triage = on
    if on:
        S.cursor = 0
    S.refresh()


def triage_bar_html(triage: bool, cursor: int, total: int) -> str:
    """The Triage-mode key legend and cursor position, or "" when the mode is
    off. Pure so the wording and the stay-silent-when-off rule are testable
    without a NiceGUI client.

    A legend is not decoration here: before this redesign the keyboard layer
    was completely undiscoverable (nothing on screen mentioned A/D/J/K) AND
    always live, which is the worst pairing - invisible but able to change a
    decision. Making the keys visible is half of what makes gating them on an
    explicit mode acceptable; the position readout is the other half, because
    at 84-126 cards "which one is the cursor on" is otherwise a scroll hunt."""
    if not triage:
        return ""
    keys = [("J", "next"), ("K", "prev"), ("A", "accept"), ("D", "decline"), ("Esc", "exit")]
    cells = "".join(f'<span class="tb-key">{k}</span><span class="tb-what">{w}</span>'
                    for k, w in keys)
    where = f"Card {min(cursor + 1, total)} of {total}" if total else "No cards in this filter"
    return (f'<span class="tb-on">Triage mode</span>{cells}'
            f'<span class="tb-where">{_esc(where)}</span>')


def triage_bar(total: int) -> None:
    inner = triage_bar_html(S.triage, S.cursor, total)
    if inner:
        ui.html(f'<div class="triage-bar">{inner}</div>').classes("w-full")


def _scroll_cursor_into_view() -> None:
    """A keyboard cursor you have to scroll to find is not a keyboard path.
    Runs once per refresh while Triage mode is on; purely cosmetic, so a
    failure here must never take the whole review render down with it."""
    def scroll() -> None:
        try:
            ui.run_javascript("document.querySelector('.review-card.current')"
                              "?.scrollIntoView({block:'nearest'})")
        except Exception:  # noqa: BLE001 - cosmetic only, never fail the render
            pass
    try:
        ui.timer(0.05, scroll, once=True)
    except Exception:  # noqa: BLE001
        pass


def _bulk(accept: bool) -> None:
    """Applies only to the active filter's entries - S.entries would silently
    accept/decline files the user can't currently see, a mis-click risk on a
    filtered view. Error-status entries are never accepted, even by Accept All -
    they have no valid destination to send to the exe."""
    for e in S.filtered():
        if accept and e.get("status", "").lower() == "error":
            continue
        S.decisions[e["filename"]] = accept
    S.refresh()


def review_card(e: dict, idx: int = 0) -> None:
    is_error = e.get("status", "").lower() == "error"
    accepted = False if is_error else S.decisions.get(e["filename"], True)
    card_cls = "review-card"
    if not accepted:
        card_cls += " declined"
    if S.triage and idx == S.cursor:
        card_cls += " current"
    with ui.element("div").classes(card_cls).style("margin-bottom:10px;"):
        # album art from the NewMusic file itself (read-only extraction) -
        # Simulate mode's sample filenames don't exist in NewMusic, and the
        # banner promises no NewMusic file is read, so skip the lookup
        # entirely rather than let it silently stat/open a nonexistent path.
        thumb = None
        if not S.simulated:
            src_path = routing.newmusic_path(e["filename"])
            thumb = get_thumbnail(f"newmusic::{e['filename']}", str(src_path), has_art=True)
        if thumb:
            ui.html(f'<div class="cover-art md"><img src="/thumbs/{thumb.name}" alt=""></div>')
        else:
            ui.html(f'<div class="cover-art md" style="{placeholder_style(e["filename"])}">'
                    f'{initials(e["title"], e["album"])}</div>')

        with ui.element("div"):
            ui.html(f'<div class="rc-title">{_esc(e["title"] or e["filename"])}</div>')
            artist_album = " &middot; ".join(x for x in (_esc(e["artist"]), _esc(e["album"])) if x)
            ui.html(f'<div class="rc-artist">{artist_album}</div>')
            ui.html(f'<div class="rc-route">NewMusic\\{_esc(e["filename"])} '
                    f'<span class="arrow">&rarr;</span> {_esc(e["destination"] or "(unresolved)")}</div>')
            if e["reason"]:
                ui.html(f'<div class="rc-reason">{_esc(e["reason"])}</div>')
            if e["tagChanges"]:
                chips = "".join(f'<span class="tag-change">{_esc(t)}</span>' for t in e["tagChanges"][:6])
                ui.html(f'<div class="rc-tags">{chips}</div>')
            badges = []
            if e["isNewFolder"]:
                badges.append('<span class="rc-badge newfolder">New folder</span>')
            if e["inBatchDuplicate"]:
                badges.append('<span class="rc-badge dupe">In-batch duplicate</span>')
            if e["libraryDuplicate"]:
                badges.append('<span class="rc-badge libdupe">Library duplicate</span>')
            if e.get("compilationAlbum"):
                # Scan-ahead found 3+ distinct primary artists on this album in-batch.
                # Shown regardless of final route: an album can be a batch compilation
                # and still land under Artists/ when the primary artist has a folder.
                badges.append('<span class="rc-badge compilation">Compilation album</span>')
            st = e["status"].lower()
            if st and st not in ("ok", "clean", "route", "moved"):
                badges.append(f'<span class="rc-badge err">{_esc(e["status"])}</span>')
            # Deliberately no "Clean route" badge for the no-exceptions case -
            # an absent badge row IS the clean signal (IDEAS.md: a badge on
            # every uneventful card trains the eye to skip the row where the
            # real exceptions - duplicates, new folders, errors - live).
            if badges:
                ui.html(f'<div class="rc-badges">{"".join(badges)}</div>')
            if e["libraryDuplicate"]:
                dup_resolution_control(e)
            if not accepted:
                ui.html('<div class="rc-declined-tag">Stays in NewMusic</div>')

        with ui.element("div").classes("rc-decision"):
            if is_error:
                ui.html('<button class="rc-toggle err" disabled aria-disabled="true" '
                        'title="Cannot accept: unresolved error">&#10005; Declined (error)</button>')
            else:
                # ONE control, not two. Accept/decline is a binary with a
                # default (accepted), so a second always-visible button spends
                # card width restating the state the first one already shows -
                # and on an accepted card the Accept button is a no-op the eye
                # still has to read. The toggle shows the state it is in and
                # flips on click, so an undo is the same click again in the
                # same place rather than a hunt for the opposite button.
                btn = ui.html(
                    f'<button class="rc-toggle {"on" if accepted else "off"}" '
                    f'aria-pressed="{"true" if accepted else "false"}" '
                    f'title="{"Click to decline - leaves the file in NewMusic" if accepted else "Click to accept"}">'
                    f'{"&#10003; Accepted" if accepted else "&#10005; Declined"}</button>')
                btn.on("click", lambda _, f=e["filename"]: _toggle_decision(f))


DUP_OPTIONS = [("D", "Delete new file"), ("L", "Delete library copy"), ("K", "Keep both")]


def dup_resolution_control(e: dict) -> None:
    """Per-duplicate resolution control shown on a library-duplicate review
    card - full CLI parity with MusicIntegrator.cs's "Duplicate Found" menu:
    the library copy's mirror path/track/album, the new file's album, the
    exe's own recommendation and reason, plus a D/L/K choice defaulting to
    that recommendation (IntegrationState.dup_resolution). Resolved here at
    review time, not as a live prompt during Execute - the exe always runs
    --no-input; this control's choice reaches it via the manifest's
    dupResolution field (see _write_manifest)."""
    current = S.dup_resolution(e)
    with ui.element("div").classes("rc-dup"):
        info = (
            f'<div class="rc-dup-info"><b>Library duplicate</b><br>'
            f'Library copy: {_esc(e["dupLibraryPath"] or "(unknown)")} '
            f'&middot; {_esc(e["dupLibraryTrack"])} &middot; {_esc(e["dupLibraryAlbum"])}<br>'
            f'New file album: {_esc(e["dupNewAlbum"])}<br>'
            f'Recommendation: <b>{_esc(e["dupRecommendation"] or "Keep both")}</b>'
            + (f' - {_esc(e["dupReason"])}' if e["dupReason"] else "")
            + "</div>"
        )
        ui.html(info)
        with ui.element("div").classes("rc-dup-resolve"):
            for key, label in DUP_OPTIONS:
                btn = ui.html(f'<button class="dupopt{" on" if current == key else ""}">{_esc(label)}</button>')
                btn.on("click", lambda _, f=e["filename"], k=key: _set_dup_resolution(f, k))


def _set_dup_resolution(filename: str, key: str) -> None:
    S.dup_resolutions[filename] = key
    S.refresh()


def _decide(filename: str, accept: bool) -> None:
    """Error-status entries can never be accepted, even via a stray call - the
    accepted property already filters them out, but the entries list is the
    single source of truth so this guard can't drift out of sync with it."""
    entry = next((en for en in S.entries if en["filename"] == filename), None)
    if accept and entry and entry.get("status", "").lower() == "error":
        return
    S.decisions[filename] = accept
    S.refresh()


def _toggle_decision(filename: str) -> None:
    """Flip one card's accept/decline. The mouse's single-toggle counterpart
    to the keyboard's explicit A/D: both funnel through _decide(), so the
    error guard and the accepted/declined partition have exactly one
    implementation. Deliberately NOT bound to a key - when you are moving
    through 100+ cards, A and D are idempotent and you always know which
    state you produced, whereas a toggle key's outcome depends on the state
    you did not stop to read."""
    current = S.decisions.get(filename, True)
    _decide(filename, not current)


# -------------------------------------------------------- stage 3 confirm


def batch_summary_html(summary: dict) -> str:
    """The batch scan-ahead strip's inner HTML, or "" when there is nothing to
    say. Pure and separated from rendering so the wording and the "stay silent
    when empty" rule are testable without a NiceGUI client.

    Three things the exe already computes and the GUI used to drop on the floor
    (docs/Development/IDEAS.md "Scan-ahead batch context is invisible"):

    - Route distribution, a different axis from the confirm bar's
      artists/new-folders/duplicates counts - it says where the batch LANDS.
    - Misc auto-migration, the only notice anywhere that a run will move files
      ALREADY in the library (existing Misc songs of an artist crossing the
      3-song threshold), not just the incoming ones. Highest-stakes line here.
    - Compilation albums detected in-batch, otherwise visible only by reading a
      destination path carefully.
    """
    routes = summary.get("routes") or {}
    migrations = summary.get("miscAutoMigrations") or []
    comps = summary.get("compilationAlbums") or []
    if not routes and not migrations and not comps:
        return ""

    parts = []
    if routes:
        ordered = sorted(routes.items(), key=lambda kv: (-kv[1], kv[0].lower()))
        cells = f'<span class="bs-sep">|</span>'.join(
            f'{_esc(name)}<span class="bs-route">: {count}</span>' for name, count in ordered)
        parts.append(f'<span class="bs-routes">Routes</span> &nbsp;{cells}')

    if migrations:
        total = summary.get("miscAutoMigrationTotal") or sum(m["count"] for m in migrations)
        who = ", ".join(f'{_esc(m["artist"])} ({m["count"]})' for m in migrations[:6])
        if len(migrations) > 6:
            who += f", +{len(migrations) - 6} more"
        parts.append(
            f'<span class="bs-migrate">Misc auto-migration: {total} existing library song(s) '
            f"will be moved out of Miscellaneous Songs into new artist folders - {who}</span>")

    if comps:
        named = ", ".join(f"'{_esc(a)}'" for a in comps[:4])
        if len(comps) > 4:
            named += f", +{len(comps) - 4} more"
        parts.append(f'<span class="bs-comp">Compilation album(s) detected: {named}</span>')

    return "".join(parts)


def batch_summary_strip() -> None:
    inner = batch_summary_html(S.summary)
    if inner:
        ui.html(f'<div class="batch-summary">{inner}</div>').classes("w-full")


def confirm_bar() -> None:
    accepted, declined = S.accepted, S.declined
    n_new = sum(1 for e in accepted if e["isNewFolder"])
    n_dupe = sum(1 for e in accepted if e["inBatchDuplicate"])
    artists = len({e["artist"] for e in accepted if e["artist"]})

    with ui.element("div").classes("panel w-full").style(
            "margin-top:16px;display:flex;justify-content:space-between;align-items:center;"
            "flex-wrap:wrap;gap:12px;"):
        summary = (f'<b style="color:var(--text)">{len(S.entries)} tracks scanned</b> &middot; '
                   f"{len(accepted)} accepted, {len(declined)} declined &middot; "
                   f"{artists} artists &middot; {n_new} new folders &middot; {n_dupe} duplicates")
        ui.html(f'<div style="font-size:12px;color:var(--text-dim);">{summary}</div>')

        if runner.busy:
            ui.html('<div class="note" style="margin:0;"><span class="spin"></span>working&hellip;</div>')
        elif not accepted:
            ui.html('<div class="note" style="margin:0;">Nothing accepted - accept at least one track '
                    "to integrate, or re-scan.</div>")
        else:
            with ui.row().style("gap:12px;align-items:center;flex-wrap:wrap;"):
                if declined:
                    ui.html('<div class="note" style="margin:0;max-width:380px;">'
                            f"{len(declined)} declined track(s) stay untouched in NewMusic "
                            "(manifest-based selective integration).</div>")
                label = (f"Integrate {len(accepted)} accepted tracks" if declined
                         else f"Integrate all {len(accepted)} tracks")
                ui.button(label, on_click=_confirm_execute).props("unelevated color=primary")


def _confirm_note_class(simulated: bool) -> str:
    """CSS class for the confirm-dialog's risk note. Both branches must carry
    the loud warning treatment - the real-run note is the single highest-stakes
    confirmation in the GUI (it moves real files) and must never fall back to
    plain dim `.note` text just because the simulated branch got the loud
    styling first. See docs/Development/HISTORY.md for the styling-parity fix."""
    return "note simulated" if simulated else "note real"


def _confirm_execute() -> None:
    with ui.dialog() as dlg, ui.card().style(
            "background:var(--panel);color:var(--text);padding:20px;max-width:480px;gap:12px;"):
        ui.label("Run simulated integration?" if S.simulated else "Run real integration?") \
            .style("font-weight:600;font-size:15px;")
        if S.simulated:
            ui.label(
                "SIMULATED - no real exe call, no NewMusic file is read or moved. This just replays "
                "sample output through the same status/summary logic a real run would use."
            ).classes(_confirm_note_class(True)).style("margin:0;")
        else:
            declined_note = (f" The {len(S.declined)} declined track(s) are excluded via manifest and stay "
                             "in NewMusic." if S.declined else "")
            ui.label(
                f"This MOVES {len(S.accepted)} file(s) from NewMusic into the library - the same "
                "operation as the CLI's y/N confirm, protected by the exe's own pre-integration "
                f"safety gate (stale mirror / dirty LibChecker blocks the run).{declined_note}"
            ).classes(_confirm_note_class(False)).style("margin:0;")
        with ui.row().classes("w-full justify-end").style("gap:10px;"):
            ui.button("Cancel", icon="close", on_click=dlg.close).props("flat color=grey")

            async def go():
                dlg.close()
                if S.simulated:
                    await run_execute_simulated()
                else:
                    await run_execute()

            ui.button("Integrate", icon="play_arrow", on_click=go).props("unelevated color=primary")
    dlg.open()


# -------------------------------------------------------- stage 4 execute


def _write_manifest(targets: list[dict]) -> list[str]:
    """Write the accepted set to the manifest file and return the ["integrate",
    "--manifest", path] args. Always called, even with zero declines: the
    accepted set at review time is the reviewed set, and a bare
    `integrate --no-input` would re-scan NEWMUSIC_DIR from scratch, picking up
    anything that arrived after the dry run with no review at all.

    A library-duplicate entry additionally carries a "dupResolution" key -
    S.dup_resolution(e), which defaults to the exe's own recommendation when
    the review card was left untouched (same lookup the unresolved-default
    fallback uses, see IntegrationState.dup_resolution) - so
    MusicIntegrator.GetDupResolution on the exe side can honour the review-
    stage choice on a real (noInput) run. Every other entry's manifest shape
    is unchanged: this never affects which files are written (still exactly
    `targets`, i.e. the accepted set the caller passed in) or removes/adds an
    entry - it only ever adds one extra key to a duplicate's own object.

    Raises OSError on write failure - the caller decides how to surface it."""
    manifest_path = config.CACHE_DIR / "accepted-manifest.json"
    config.CACHE_DIR.mkdir(parents=True, exist_ok=True)
    entries = []
    for e in targets:
        entry = {"filename": e["filename"], "artist": e["artist"], "title": e["title"]}
        if e.get("libraryDuplicate"):
            entry["dupResolution"] = S.dup_resolution(e)
        entries.append(entry)
    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump(entries, f, indent=2)
    return ["integrate", "--manifest", str(manifest_path)]


def _open_run_log() -> "object | None":
    """Open a timestamped log file under the cache dir for this run's exe
    output. The GUI's own record (S.exec_lines) is capped and cleared on every
    run, so this is the only durable copy the GUI keeps of what the exe said."""
    config.RUN_LOGS_DIR.mkdir(parents=True, exist_ok=True)
    log_path = config.RUN_LOGS_DIR / f"integration-{datetime.now():%Y%m%d-%H%M%S}.log"
    try:
        return open(log_path, "w", encoding="utf-8")
    except OSError:
        return None


async def run_execute() -> None:
    if runner.busy:
        ui.notify("Another operation is already running", type="warning")
        return
    targets = S.accepted
    try:
        args = _write_manifest(targets)
    except OSError as e:
        ui.notify(f"Could not write manifest: {e}", type="negative")
        return

    S.stage = 4
    S.exec_lines = []
    S.exec_done = False
    S.exec_ok = None
    S.exec_summary = ""
    S.exec_targets = targets
    S.exec_status = {e["filename"]: "queued" for e in targets}
    S.refresh()

    throttle = {"pending": False}
    log_file = _open_run_log()

    def on_line(line: str) -> None:
        S.exec_lines.append(line)
        _update_exec_status(line)
        if log_file:
            try:
                log_file.write(line + "\n")
                log_file.flush()
            except OSError:
                pass
        if not throttle["pending"]:
            throttle["pending"] = True

            def flush():
                throttle["pending"] = False
                S.refresh()
            ui.timer(0.5, flush, once=True)

    try:
        result = await runner.run(
            args,
            action="Integration",
            on_line=on_line,
            timeout=config.TIMEOUT_INTEGRATE,
        )
    finally:
        if log_file:
            log_file.close()
    _finish_execute(result)


def _finish_execute(result: RunResult) -> None:
    """Shared post-run finalization for both a real run_execute() and the
    synthetic run_execute_simulated() - both produce a RunResult and must be
    interpreted identically so a simulated run exercises the real UI logic."""
    S.exec_done = True
    S.exec_ok = result.ok
    S.confidence_report = routing.parse_confidence_report(result.lines)
    show_modal = False
    if result.ok:
        failed = sum(1 for v in S.exec_status.values() if v == "failed")
        for k, v in S.exec_status.items():
            if v in ("queued", "moving"):
                S.exec_status[k] = "done"
        moved = sum(1 for v in S.exec_status.values() if v == "done")
        skipped_note = f", {len(S.declined)} declined left in NewMusic" if S.declined else ""
        S.exec_summary = (f"Integration complete - {moved} moved"
                          + (f", {failed} skipped/failed" if failed else "")
                          + skipped_note
                          + ". Statistics will reflect the new batch after the next analysis run.")
    else:
        S.exec_summary = result.interpreted("Integration")
        failed_name = _failed_filename_from_output(result.lines) if not result.cancelled else None
        n_notrun = 0
        for k, v in S.exec_status.items():
            if v not in ("queued", "moving"):
                continue
            if failed_name and k == failed_name:
                S.exec_status[k] = "failed"
            else:
                S.exec_status[k] = "notrun"
                n_notrun += 1
        # failed_name is already named in result.interpreted()'s cause line above -
        # repeating it here would duplicate the filename with no separator.
        if n_notrun:
            S.exec_summary += f" {n_notrun} file(s) were not attempted."
        show_modal = not result.cancelled
    # Refresh BEFORE opening the error modal: S.refresh() rebuilds the
    # @ui.refreshable slot this function is called from, which would destroy
    # a dialog created inside it a moment earlier - opening the modal after
    # the rebuild lets it survive.
    S.refresh()
    if show_modal:
        show_error_modal("Integration", result)


async def run_execute_simulated() -> None:
    """Synthetic execute for Simulate mode: no exe subprocess, no manifest
    file, no NewMusic access - feeds the exe's real per-line output formats
    through the same on_line/_update_exec_status path a real run uses (so
    known bugs like _update_exec_status not matching '[AUTO]' lines are
    faithfully reproduced), then finishes through the same _finish_execute()
    a real run uses. Error-status entries are never in S.accepted (see
    IntegrationState.accepted), so this never sees one - there is nothing
    left in Simulate mode that can fail mid-batch."""
    if runner.busy:
        ui.notify("Another operation is already running", type="warning")
        return
    targets = S.accepted
    S.stage = 4
    S.exec_lines = []
    S.exec_done = False
    S.exec_ok = None
    S.exec_summary = ""
    S.exec_targets = targets
    S.exec_status = {e["filename"]: "queued" for e in targets}
    S.refresh()

    lines: list[str] = []
    for e in targets:
        await asyncio.sleep(SIMULATE_STEP_DELAY)
        line = f"[AUTO] {e['artist']} - {e['title']}"
        lines.append(line)
        _update_exec_status(line)
        S.exec_lines.append(line)
        S.refresh()

    moved = len(targets)
    lines += [
        "CONFIDENCE REPORT",
        f"  Files in NewMusic: {len(targets)}  |  Moved: {moved}  |  Skipped: 0",
        f"  Sanity check: all {moved} moved file(s) exist and are readable.",
    ]
    result = RunResult(command=["integrate", "--simulate"], returncode=0, lines=lines)
    _finish_execute(result)


def _failed_filename_from_output(lines: list[str]) -> str | None:
    """The exe processes targets one at a time and halts on the first error,
    printing 'Error processing file: <filename>' before stopping - the only
    line in its output that names which specific file failed (everything
    else, e.g. `[AUTO]`/`[SKIP]`, prints artist/title text, not a filename).
    Everything still queued/moving after this file is therefore genuinely
    unattempted, not failed."""
    prefix = "Error processing file:"
    for line in lines:
        idx = line.find(prefix)
        if idx != -1:
            name = line[idx + len(prefix):].strip()
            if name:
                return name
    return None


EXEC_STATUS_LABELS = {
    "queued": "queued", "moving": "moving", "done": "done",
    "failed": "failed", "notrun": "not run",
}


def _update_exec_status(line: str) -> None:
    """Structured progress from the exe's REAL per-track output.

    The exe never prints filenames for a success/skip - only tag text via
    `[AUTO] {Artists} - {Title}` (moved) and `[SKIP] {Artists} - {Title}`
    (left in place), one line per file, after it's already done (there's no
    separate "started processing" line, so there's no observable "moving"
    state - a file goes straight from queued to done/failed). The one line
    that DOES carry a filename is the halt-on-error line,
    `Error processing file: {filename}`, handled separately below.

    A match only counts if no OTHER target's "artist - title" text
    containing it as a substring also appears in the line - otherwise one
    track's text matching inside another's would flip the wrong status."""
    low = line.lower()

    error_prefix = "error processing file:"
    idx = low.find(error_prefix)
    if idx != -1:
        failed_name = line[idx + len(error_prefix):].strip().lower()
        for e in S.exec_targets:
            if e["filename"].lower() == failed_name:
                S.exec_status[e["filename"]] = "failed"
        return

    is_done = "[auto]" in low or "[skip]" in low
    if not is_done:
        return

    def tag_text(e: dict) -> str:
        return f"{e.get('artist', '')} - {e.get('title', '')}".lower()

    candidates = [e for e in S.exec_targets if tag_text(e) and tag_text(e) in low]
    texts = [tag_text(c) for c in candidates]
    matches = [c for c in candidates
               if not any(tag_text(c) != other and tag_text(c) in other
                          for other in texts)]
    for e in matches:
        S.exec_status[e["filename"]] = "done"


def stage_execute() -> None:
    total = max(1, len(S.exec_targets))
    done = sum(1 for v in S.exec_status.values() if v in ("done", "failed"))
    moving = sum(1 for v in S.exec_status.values() if v == "moving")
    pct = 100 if S.exec_done else int(100 * (done + 0.5 * moving) / total)

    with ui.element("div").classes("panel w-full"):
        with ui.element("div").classes("panel-title"):
            label = "Integration result" if S.exec_done else "Integrating&hellip;"
            ui.html(f"<span>{label}</span>")
            if not S.exec_done:
                ui.button("Cancel", icon="cancel", on_click=lambda: runner.cancel()) \
                    .props("outline dense color=negative size=sm")
        fill_class = "fill fail" if S.exec_ok is False else "fill"
        ui.html(f'<div class="progress-track"><div class="{fill_class}" style="width:{pct}%;"></div></div>')

        rows = []
        for e in S.exec_targets:
            st = S.exec_status.get(e["filename"], "queued")
            dest = _esc(e["destination"]) if e["destination"] else ""
            arrow = f' <span style="color:var(--text-dim)">&rarr;</span> {dest}' if dest else ""
            rows.append(f'<div class="progress-row"><span class="st st-{st}">{EXEC_STATUS_LABELS[st]}</span>'
                        f'<span class="pr-name">{_esc(e["filename"])}{arrow}</span></div>')
        ui.html('<div style="margin-top:12px;max-height:340px;overflow:auto;width:100%;">'
                + "".join(rows) + "</div>")

        if S.exec_done:
            ui.html(f'<div class="note" style="margin-top:12px;">{_esc(S.exec_summary)}</div>')
            confidence_report_strip()

            def reset():
                S.stage = 1
                S.entries = []
                S.simulated = False
                S.refresh()

            ui.button("New scan", icon="restart_alt", on_click=reset).props("outline dense color=primary size=sm") \
                .style("margin-top:10px;")


# --------------------------------------------------------- advanced log


def advanced_log() -> None:
    lines = S.exec_lines if S.stage >= 4 else S.scan_lines
    if not lines:
        return
    with ui.expansion("Advanced / raw output (debug)").classes("w-full") \
            .style("margin-top:16px;border-top:1px solid var(--panel-border);"):
        tail = "\n".join(lines[-300:])
        ui.html(f'<div class="console" style="max-height:220px;">{_esc(tail)}</div>')


def _esc(text: str) -> str:
    return html.escape(text, quote=True)

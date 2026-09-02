"""Integration tab - GUI-native staged workflow (NOT a terminal).

Stage 1  Scan       integrate --dry-run --no-input --json-output -> routing JSON
Stage 2  Review     one card per proposed track: art, destination, reason,
                    tag-change chips, badges, Accept/Decline
Stage 3  Confirm    summary bar + single primary action
Stage 4  Execute    real integrate --no-input with structured per-track
                    progress parsed from the exe's live output

Per-track selective execution is real: when tracks are declined, the GUI
writes the accepted set to gui/.cache/accepted-manifest.json and runs
`integrate --manifest <path> --no-input` - the exe moves only the accepted
files and leaves declined ones untouched in NewMusic (the GUI itself never
moves or deletes NewMusic files; the exe owns all file operations).
"""
from __future__ import annotations

import asyncio
import html
import json

from nicegui import ui

from gui import config, routing
from gui.art import get_thumbnail, initials, placeholder_style
from gui.components.error_modal import show_error_modal
from gui.runner import runner


class IntegrationState:
    def __init__(self):
        self.stage = 1                      # 1 scan, 2 review, 3/4 execute
        self.entries: list[dict] = []
        self.decisions: dict[str, bool] = {}   # filename -> accepted
        self.filter = "all"                 # all | conflicts | newfolders
        self.scan_lines: list[str] = []
        self.exec_lines: list[str] = []
        self.exec_status: dict[str, str] = {}  # filename -> queued/moving/done/failed
        self.exec_targets: list[dict] = []     # the accepted entries being executed
        self.exec_done = False
        self.exec_summary = ""
        self.refresh = lambda: None

    @property
    def accepted(self) -> list[dict]:
        return [e for e in self.entries if self.decisions.get(e["filename"], True)]

    @property
    def declined(self) -> list[dict]:
        return [e for e in self.entries if not self.decisions.get(e["filename"], True)]

    def filtered(self) -> list[dict]:
        if self.filter == "conflicts":
            return [e for e in self.entries
                    if e["inBatchDuplicate"] or e["status"].lower() not in ("", "ok", "clean", "moved", "route")]
        if self.filter == "newfolders":
            return [e for e in self.entries if e["isNewFolder"]]
        return self.entries


S = IntegrationState()


def build() -> None:
    with ui.element("header").classes("page"):
        ui.html("<h1>Integration</h1>")
        ui.html('<div class="meta">Staged review of NewMusic - scan, review routing, confirm, integrate</div>')

    @ui.refreshable
    def content():
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
    steps = [("Scan NewMusic", 1), ("Review routing", 2), ("Confirm", 3), ("Integrate", 4)]
    html = ['<div class="stepper">']
    for label, n in steps:
        cls = "done" if S.stage > n else ("active" if S.stage == n else "")
        mark = "&#10003;" if S.stage > n else str(n)
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
                ui.button("Cancel", on_click=lambda: runner.cancel()).props("outline color=negative dense")
            else:
                ui.button("Scan NewMusic", on_click=lambda: asyncio.create_task(run_scan())) \
                    .props("unelevated color=primary")


async def run_scan() -> None:
    if runner.busy:
        ui.notify("Another operation is already running", type="warning")
        return
    S.scan_lines = []
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
    if path:
        try:
            entries = routing.parse_routing_file(path)
        except (OSError, ValueError) as e:
            ui.notify(f"Could not parse routing JSON: {e}", type="negative")
    S.entries = entries
    S.decisions = {e["filename"]: True for e in entries}
    S.exec_status = {}
    S.exec_done = False
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
            ui.button("Re-scan", on_click=lambda: asyncio.create_task(run_scan())) \
                .props("outline color=primary dense")
        return

    n_new = sum(1 for e in S.entries if e["isNewFolder"])
    n_dupe = sum(1 for e in S.entries if e["inBatchDuplicate"])

    with ui.row().classes("w-full items-center justify-between").style("margin-bottom:12px;flex-wrap:wrap;gap:10px;"):
        with ui.row().style("gap:8px;flex-wrap:wrap;"):
            for key, label in [("all", f"All ({len(S.entries)})"),
                               ("newfolders", f"New folders ({n_new})"),
                               ("conflicts", f"Duplicates / conflicts ({n_dupe})")]:
                cls = "chip active" if S.filter == key else "chip"
                chip = ui.html(f'<div class="{cls}">{label}</div>')
                chip.on("click", lambda _, k=key: _set_filter(k))
        with ui.row().style("gap:8px;"):
            ui.button("Accept all", on_click=lambda: _bulk(True)).props("outline dense color=positive size=sm")
            ui.button("Decline all", on_click=lambda: _bulk(False)).props("outline dense color=negative size=sm")
            ui.button("Re-scan", on_click=lambda: asyncio.create_task(run_scan())) \
                .props("outline dense color=grey size=sm")

    for e in S.filtered():
        review_card(e)

    confirm_bar()


def _set_filter(k: str) -> None:
    S.filter = k
    S.refresh()


def _bulk(accept: bool) -> None:
    for e in S.entries:
        S.decisions[e["filename"]] = accept
    S.refresh()


def review_card(e: dict) -> None:
    accepted = S.decisions.get(e["filename"], True)
    card_cls = "review-card" + ("" if accepted else " declined")
    with ui.element("div").classes(card_cls).style("margin-bottom:10px;"):
        # album art from the NewMusic file itself (read-only extraction)
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
            st = e["status"].lower()
            if st and st not in ("ok", "clean", "route", "moved"):
                badges.append(f'<span class="rc-badge err">{_esc(e["status"])}</span>')
            if not badges:
                badges.append('<span class="rc-badge clean">Clean route</span>')
            ui.html(f'<div class="rc-badges">{"".join(badges)}</div>')

        with ui.element("div").classes("rc-decision"):
            acc = ui.html(f'<button class="accept{" on" if accepted else ""}">&#10003; Accept</button>')
            dec = ui.html(f'<button class="decline{"" if accepted else " on"}">&#10005; Decline</button>')
            acc.on("click", lambda _, f=e["filename"]: _decide(f, True))
            dec.on("click", lambda _, f=e["filename"]: _decide(f, False))


def _decide(filename: str, accept: bool) -> None:
    S.decisions[filename] = accept
    S.refresh()


# -------------------------------------------------------- stage 3 confirm


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


def _confirm_execute() -> None:
    with ui.dialog() as dlg, ui.card().style(
            "background:var(--panel);color:var(--text);padding:20px;max-width:480px;gap:12px;"):
        ui.label("Run real integration?").style("font-weight:600;font-size:15px;")
        declined_note = (f" The {len(S.declined)} declined track(s) are excluded via manifest and stay "
                         "in NewMusic." if S.declined else "")
        ui.label(
            f"This MOVES {len(S.accepted)} file(s) from NewMusic into the library - the same "
            "operation as the CLI's y/N confirm, protected by the exe's own pre-integration "
            f"safety gate (stale mirror / dirty LibChecker blocks the run).{declined_note}"
        ).classes("note").style("margin:0;")
        with ui.row().classes("w-full justify-end").style("gap:10px;"):
            ui.button("Cancel", on_click=dlg.close).props("flat color=grey")

            async def go():
                dlg.close()
                await run_execute()

            ui.button("Integrate", on_click=go).props("unelevated color=primary")
    dlg.open()


# -------------------------------------------------------- stage 4 execute


def _write_manifest(targets: list[dict]) -> list[str]:
    """Write the accepted set to the manifest file and return the ["integrate",
    "--manifest", path] args. Always called, even with zero declines: the
    accepted set at review time is the reviewed set, and a bare
    `integrate --no-input` would re-scan NEWMUSIC_DIR from scratch, picking up
    anything that arrived after the dry run with no review at all.

    Raises OSError on write failure - the caller decides how to surface it."""
    manifest_path = config.CACHE_DIR / "accepted-manifest.json"
    config.CACHE_DIR.mkdir(parents=True, exist_ok=True)
    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump([{"filename": e["filename"], "artist": e["artist"],
                    "title": e["title"]} for e in targets], f, indent=2)
    return ["integrate", "--manifest", str(manifest_path)]


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
    S.exec_summary = ""
    S.exec_targets = targets
    S.exec_status = {e["filename"]: "queued" for e in targets}
    S.refresh()

    throttle = {"pending": False}

    def on_line(line: str) -> None:
        S.exec_lines.append(line)
        _update_exec_status(line)
        if not throttle["pending"]:
            throttle["pending"] = True

            def flush():
                throttle["pending"] = False
                S.refresh()
            ui.timer(0.5, flush, once=True)

    result = await runner.run(
        args,
        action="Integration",
        on_line=on_line,
        timeout=config.TIMEOUT_INTEGRATE,
    )
    S.exec_done = True
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
        for k, v in S.exec_status.items():
            if v in ("queued", "moving"):
                S.exec_status[k] = "failed"
        if not result.cancelled:
            show_error_modal("Integration", result)
    S.refresh()


def _update_exec_status(line: str) -> None:
    """Best-effort structured progress: a filename appearing in the exe's
    output means that file is being processed; [MOVED]/[SKIPPED] confidence
    report lines settle the final state.

    A filename match only counts if no OTHER target's filename containing it
    as a substring also appears in the line - otherwise "Song.mp3" would match
    inside "Another Song.mp3" and flip the wrong track's status."""
    low = line.lower()
    candidates = [e for e in S.exec_targets if e["filename"].lower() in low]
    names = [c["filename"].lower() for c in candidates]
    matches = [c for c in candidates
               if not any(c["filename"].lower() != other and c["filename"].lower() in other
                          for other in names)]
    for e in matches:
        fn = e["filename"]
        if "[moved]" in low or "moved:" in low:
            S.exec_status[fn] = "done"
        elif "[skipped]" in low or "[error]" in low or "[failed]" in low:
            S.exec_status[fn] = "failed"
        elif S.exec_status.get(fn) == "queued":
            S.exec_status[fn] = "moving"


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
                ui.button("Cancel", on_click=lambda: runner.cancel()).props("outline dense color=negative size=sm")
        ui.html(f'<div class="progress-track"><div class="fill" style="width:{pct}%;"></div></div>')

        rows = []
        for e in S.exec_targets:
            st = S.exec_status.get(e["filename"], "queued")
            dest = _esc(e["destination"]) if e["destination"] else ""
            arrow = f' <span style="color:var(--text-dim)">&rarr;</span> {dest}' if dest else ""
            rows.append(f'<div class="progress-row"><span class="st st-{st}">{st}</span>'
                        f'<span class="pr-name">{_esc(e["filename"])}{arrow}</span></div>')
        ui.html('<div style="margin-top:12px;max-height:340px;overflow:auto;width:100%;">'
                + "".join(rows) + "</div>")

        if S.exec_done:
            ui.html(f'<div class="note" style="margin-top:12px;">{_esc(S.exec_summary)}</div>')

            def reset():
                S.stage = 1
                S.entries = []
                S.refresh()

            ui.button("New scan", on_click=reset).props("outline dense color=primary size=sm") \
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

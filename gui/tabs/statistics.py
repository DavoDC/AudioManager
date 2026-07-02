"""Statistics tab - the centerpiece. Every panel maps 1:1 to a field in
logs/analysis-stats.json (via data_loader). Includes the data-freshness
control (the demoted Analysis function): Re-run analysis + Force full regen.

Global date filter semantics (windowed panels only - Year/Decade/Age/batch):
- Year windows filter the C#-computed yearDistribution by label; Age buckets
  are derived from that same windowed yearDistribution (no per-track
  re-aggregation - genre/cover stats stay exactly as C# computed them).
- 'Last integration batch' resolves the latest git batch's tracks via
  tracks.json rows (contract data, not XML).
"""
from __future__ import annotations

import datetime

from nicegui import ui

from gui import config, theme
from gui.components import charts
from gui.components.error_modal import show_error_modal
from gui.data_loader import fmt_bytes, fmt_int, fmt_mmss, relative_time
from gui.runner import runner
from gui.state import state

WINDOW_OPTIONS = ["All time", "2026", "2025", "Last integration batch"]


# ------------------------------------------------------ windowing helpers


def _year_of(label: str) -> int | None:
    try:
        return int(label)
    except (TypeError, ValueError):
        return None


def _windowed_years(window: str) -> list[dict]:
    """yearDistribution filtered to the selected window."""
    if not state.stats:
        return []
    dist = state.stats.year_distribution
    if window == "All time":
        return dist
    if window == "Last integration batch":
        counts: dict[str, int] = {}
        if state.batches:
            for t in state.batch_tracks(state.batches[0]):
                y = t.get("year")
                if isinstance(y, str) and _year_of(y) is not None:
                    counts[y] = counts.get(y, 0) + 1
        return sorted(
            ({"label": y, "count": c} for y, c in counts.items()),
            key=lambda d: -d["count"],
        )
    sel = _year_of(window)
    return [d for d in dist if _year_of(d["label"]) == sel]


def _windowed_decades(window: str) -> list[dict]:
    if not state.stats:
        return []
    if window == "All time":
        return state.stats.decade_distribution
    counts: dict[str, int] = {}
    for d in _windowed_years(window):
        y = _year_of(d["label"])
        if y is not None:
            dec = f"{(y // 10) * 10}s"
            counts[dec] = counts.get(dec, 0) + d["count"]
    return sorted(
        ({"label": k, "count": v} for k, v in counts.items()),
        key=lambda d: -d["count"],
    )


AGE_BUCKETS = [("0-2y", 0, 2), ("2-5y", 2, 5), ("5-10y", 5, 10),
               ("10-20y", 10, 20), ("20y+", 20, 10_000)]


def _windowed_ages(window: str) -> list[dict]:
    """All-time: the exe's fixed buckets verbatim. Windowed: same buckets
    derived from the windowed yearDistribution (age = now - year)."""
    if not state.stats:
        return []
    if window == "All time":
        return state.stats.age_distribution
    now = datetime.date.today().year
    buckets = {label: 0 for label, _, _ in AGE_BUCKETS}
    for d in _windowed_years(window):
        y = _year_of(d["label"])
        if y is None:
            continue
        age = now - y
        for label, lo, hi in AGE_BUCKETS:
            if (age <= hi if lo == 0 else lo < age <= hi):
                buckets[label] += d["count"]
                break
    return [{"label": label, "count": buckets[label]} for label, _, _ in AGE_BUCKETS]


def _windowed_batches(window: str) -> list[dict]:
    batches = state.batches
    if window == "Last integration batch":
        return batches[:1]
    if window in ("2026", "2025"):
        return [b for b in batches if b["date"].startswith(window)]
    return batches


# ---------------------------------------------------------------- the tab


def build() -> None:
    window = {"value": "All time"}

    with ui.element("header").classes("page"):
        ui.html("<h1>Statistics Dashboard</h1>")
        with ui.row().style("align-items:center;gap:10px;"):
            ui.select(WINDOW_OPTIONS, value="All time",
                      on_change=lambda e: (_set(window, e.value), content.refresh())) \
                .props("dense outlined dark options-dark").classes("am-select") \
                .style("min-width:190px;font-size:12px;")

    freshness_bar()

    @ui.refreshable
    def content():
        if state.load_error and not state.stats:
            with ui.column().classes("panel w-full").style("align-items:center;padding:48px;gap:12px;"):
                ui.label("No statistics data yet").style(
                    "font-size:16px;font-weight:600;color:var(--text);")
                ui.label(state.load_error).classes("note").style("margin:0;")
            return
        _render(window["value"])

    content()
    state.on_reload(content.refresh)


def _set(d: dict, v) -> None:
    d["value"] = v


# ------------------------------------------------------- freshness control


def freshness_bar() -> None:
    @ui.refreshable
    def bar():
        s = state.stats
        running = runner.busy
        with ui.element("div").classes("freshness-bar"):
            if running:
                info = (f'<span class="spin"></span> <b>{runner.current_action}</b> running&hellip; '
                        f"analysis output is the Statistics data - the dashboard refreshes when it finishes")
            elif s:
                info = (f'<span class="fresh-dot">&#9679;</span> Analysis last run: '
                        f'<b>{relative_time(s.generated_at)}</b> &middot; '
                        f'{fmt_int(s.summary_num("trackCount"))} tracks &middot; '
                        f"data from <b>analysis-stats.json</b> (analysis output <i>is</i> this dashboard's data)")
            else:
                info = ('<span class="fresh-dot stale">&#9679;</span> <b>Analysis has never run</b> '
                        "- run it once to populate every panel below")
            ui.html(f'<div class="fresh-info">{info}</div>')
            with ui.row().style("gap:8px;align-items:center;"):
                if running:
                    ui.button("Cancel", on_click=lambda: runner.cancel()) \
                        .props("outline dense color=negative size=sm")
                else:
                    ui.button("Re-run analysis", on_click=lambda: _run_analysis(bar, force=False)) \
                        .props("unelevated dense color=primary size=sm")
                    ui.button("Force full regen", on_click=lambda: _confirm_force_regen(bar)) \
                        .props("outline dense color=grey size=sm")

    bar()
    state.on_reload(bar.refresh)


def _confirm_force_regen(bar) -> None:
    with ui.dialog() as dlg, ui.card().style(
            "background:var(--panel);color:var(--text);padding:20px;max-width:460px;gap:12px;"):
        ui.label("Force full regeneration?").style("font-weight:600;font-size:15px;")
        ui.label(
            "This re-reads cover art from every MP3 in the library - the slow path "
            "(minutes, not seconds). It may also change AudioMirror XML; any resulting "
            "uncommitted changes are surfaced in the Mirror tab rather than auto-committed."
        ).classes("note").style("margin:0;")
        with ui.row().classes("w-full justify-end").style("gap:10px;"):
            ui.button("Cancel", on_click=dlg.close).props("flat color=grey")

            async def go():
                dlg.close()
                await _run_analysis_impl(bar, force=True)

            ui.button("Force full regen", on_click=go).props("unelevated color=negative")
    dlg.open()


def _run_analysis(bar, force: bool) -> None:
    import asyncio
    asyncio.create_task(_run_analysis_impl(bar, force))


async def _run_analysis_impl(bar, force: bool) -> None:
    if runner.busy:
        ui.notify("Another operation is already running", type="warning")
        return
    action = "Force full regen" if force else "Analysis"
    args = ["analysis", "--json-output"]
    timeout = config.TIMEOUT_ANALYSIS
    if force:
        # Force-regen may rewrite AudioMirror XML: never auto-commit from the
        # GUI - the Mirror tab surfaces the diff instead.
        args += ["--force-regen", "--no-auto-commit"]
        timeout = config.TIMEOUT_FORCE_REGEN
    bar.refresh()
    result = await runner.run(args, action=action, timeout=timeout)
    if result.ok:
        state.load()
        ui.notify(f"{action} complete - dashboard refreshed", type="positive")
        state.notify_reload()
    else:
        bar.refresh()
        if not result.cancelled:
            show_error_modal(action, result,
                             retry=lambda: _run_analysis_impl(bar, force))
        else:
            ui.notify(f"{action} cancelled", type="warning")


# ------------------------------------------------------------- rendering


def _panel(title: str, height: int = 260):
    p = ui.element("div").classes("panel w-full")
    return p


def _echart(option: dict, height: int = 260):
    ui.echart(option).style(f"height:{height}px;width:100%;")


def _render(window: str) -> None:
    s = state.stats
    tiles(s)

    # Row 1: Genre (donut/pie/treemap) + Decade (bar/donut)
    with ui.element("div").classes("am-grid2"):
        with _panel("Genre Distribution"):
            genre_mode = {"value": "Donut"}

            @ui.refreshable
            def genre_chart():
                data = s.genre_distribution[:11]
                rest = sum(d["count"] for d in s.genre_distribution[11:])
                if rest:
                    data = data + [{"label": "Other", "count": rest}]
                m = genre_mode["value"]
                if m == "Treemap":
                    _echart(charts.treemap(data), 300)
                else:
                    _echart(charts.donut(data, pie=(m == "Pie")), 300)

            with ui.element("div").classes("panel-title"):
                ui.html("<span>Genre Distribution</span>")
                ui.select(["Donut", "Pie", "Treemap"], value="Donut",
                          on_change=lambda e: (_set(genre_mode, e.value), genre_chart.refresh())) \
                    .props("dense outlined dark options-dark").style("font-size:11px;")
            genre_chart()

        with _panel("Decade Distribution"):
            dec_mode = {"value": "Bar"}

            @ui.refreshable
            def decade_chart():
                data = sorted(_windowed_decades(window), key=lambda d: d["label"])
                if not data:
                    ui.label("No tracks in this window").classes("note")
                    return
                if dec_mode["value"] == "Donut":
                    _echart(charts.donut(data), 300)
                else:
                    _echart(charts.bar(data, theme.ACCENT), 300)

            with ui.element("div").classes("panel-title"):
                ui.html("<span>Decade Distribution</span>")
                ui.select(["Bar", "Donut"], value="Bar",
                          on_change=lambda e: (_set(dec_mode, e.value), decade_chart.refresh())) \
                    .props("dense outlined dark options-dark").style("font-size:11px;")
            decade_chart()

    # Row 2: Year (horizontal, top N + show all) + Genre balance radar
    with ui.element("div").classes("am-grid2e"):
        with _panel("Year Distribution"):
            show_all = {"value": False}

            @ui.refreshable
            def year_chart():
                data = _windowed_years(window)
                if not data:
                    ui.label("No tracks in this window").classes("note")
                    return
                shown = data if show_all["value"] else data[:12]
                _echart(charts.bar(shown, theme.ACCENT3, horizontal=True),
                        max(260, 24 * len(shown)))

            with ui.element("div").classes("panel-title"):
                ui.html("<span>Year Distribution</span>")
                with ui.element("span").classes("toggle-pair"):
                    b_top = ui.html('<button class="active">Top 12</button>')
                    b_all = ui.html("<button>Show all</button>")

                    def set_all(v: bool):
                        show_all["value"] = v
                        b_top.content = f'<button class="{"" if v else "active"}">Top 12</button>'
                        b_all.content = f'<button class="{"active" if v else ""}">Show all</button>'
                        year_chart.refresh()

                    b_top.on("click", lambda _: set_all(False))
                    b_all.on("click", lambda _: set_all(True))
            year_chart()

        with _panel("Genre Balance (radar)"):
            with ui.element("div").classes("panel-title"):
                ui.html("<span>Genre Balance (radar)</span>")
            radar_data = s.genre_distribution[:8]
            _echart(charts.radar(radar_data), 300)

    # Row 3: Top Artists (toggle) + Recent Additions (batch-grouped)
    with ui.element("div").classes("am-grid2e"):
        with _panel("Top Artists"):
            artist_mode = {"value": "exclMusivation"}

            @ui.refreshable
            def artists_chart():
                data = s.top_artists(artist_mode["value"])[:10]
                if not data:
                    ui.label("No artist data").classes("note")
                    return
                _echart(charts.bar(data, theme.ACCENT2, horizontal=True), 300)

            with ui.element("div").classes("panel-title"):
                ui.html("<span>Top Artists</span>")
                with ui.element("span").classes("toggle-pair"):
                    b_ex = ui.html('<button class="active">Excl. Musivation</button>')
                    b_all2 = ui.html("<button>All Artists</button>")

                    def set_mode(mode: str):
                        artist_mode["value"] = mode
                        ex = mode == "exclMusivation"
                        b_ex.content = f'<button class="{"active" if ex else ""}">Excl. Musivation</button>'
                        b_all2.content = f'<button class="{"" if ex else "active"}">All Artists</button>'
                        artists_chart.refresh()

                    b_ex.on("click", lambda _: set_mode("exclMusivation"))
                    b_all2.on("click", lambda _: set_mode("all"))
            artists_chart()

        with _panel("Recent Additions"):
            with ui.element("div").classes("panel-title"):
                ui.html("<span>Recent Additions (grouped by integration batch)</span>")
            recent_additions(window)

    # Row 4: per-batch bar (full width)
    with ui.element("div").classes("panel w-full").style("margin-bottom:16px;"):
        with ui.element("div").classes("panel-title"):
            ui.html("<span>Tracks Added Per Integration Batch</span>")
        batches = _windowed_batches(window)[:10][::-1]
        if batches:
            _echart(charts.bar(
                [{"label": b["date"], "count": b["count"]} for b in batches],
                theme.ACCENT), 240)
        else:
            ui.label("No integration batches found in AudioMirror history for this window.") \
                .classes("note")

    # Row 5: Age + Cover-resolution
    with ui.element("div").classes("am-grid2e"):
        with _panel("Track Age Distribution"):
            with ui.element("div").classes("panel-title"):
                ui.html("<span>Track Age Distribution</span>")
            ages = _windowed_ages(window)
            if any(d["count"] for d in ages):
                _echart(charts.bar(ages, theme.ACCENT4), 240)
            else:
                ui.label("No tracks in this window").classes("note")
            a = s.age_stats
            if a.get("averageYears") is not None:
                ui.html(
                    '<div class="note" style="margin-top:6px;">Library-wide: average age '
                    f'<b style="color:var(--text)">{a.get("averageYears")}y</b> &middot; median '
                    f'<b style="color:var(--text)">{a.get("medianYears")}y</b> &middot; newest '
                    f'<b style="color:var(--text)">{a.get("newestYears")}y</b> &middot; oldest '
                    f'<b style="color:var(--text)">{a.get("oldestYears")}y</b></div>'
                )

        with _panel("Cover Art Resolution Breakdown"):
            with ui.element("div").classes("panel-title"):
                ui.html("<span>Cover Art Resolution Breakdown</span>")
            hist = s.cover_dimension_histogram[:15]
            if hist:
                _echart(charts.bar(hist, theme.ACCENT5), 240)
            else:
                ui.label("No cover dimension data").classes("note")
            c = s.cover_art
            ui.html(
                '<div class="note" style="margin-top:6px;">'
                f'No cover: <b style="color:var(--text)">{fmt_int(c.get("noCover", 0))}</b> &middot; '
                f'sub-800px: <b style="color:var(--text)">{fmt_int(c.get("subMin800", 0))}</b> &middot; '
                f'non-square: <b style="color:var(--text)">{fmt_int(c.get("nonSquare", 0))}</b> &middot; '
                f'unreadable format: <b style="color:var(--text)">{fmt_int(c.get("unknownFormat", 0))}</b></div>'
            )

    # Row 6: the two rings (one reusable widget)
    with ui.element("div").classes("am-grid2e"):
        with _panel("Tag Completeness"):
            with ui.element("div").classes("panel-title"):
                ui.html("<span>Tag Completeness</span>")
            tc = s.tag_completeness
            _echart(charts.ring(tc.get("percent", 0), "Complete", theme.ACCENT), 230)
            ui.html(f'<div class="note">{fmt_int(tc.get("complete"))} of {fmt_int(tc.get("total"))} '
                    "tracks have Title, Artist, Album, Genre and Year all present.</div>")
        with _panel("High-Res Cover Coverage"):
            with ui.element("div").classes("panel-title"):
                ui.html("<span>High-Res Cover Art Coverage (&ge;800px)</span>")
            cc = s.cover_coverage_800
            _echart(charts.ring(cc.get("percent", 0), "≥800px", theme.ACCENT2), 230)
            ui.html(f'<div class="note">{fmt_int(cc.get("covered"))} of {fmt_int(cc.get("total"))} '
                    "tracks have cover art at least 800px on the short side "
                    "(tracks with no cover count as not covered).</div>")

    ui.html(
        '<p class="note">Windowed panels (Year, Decade, Age, batches) follow the header '
        "date filter; Genre and Cover panels are library-wide by nature. All numbers come "
        "from <b>analysis-stats.json</b>, computed by the C# Analyser - the GUI parses no XML.</p>"
    )


def tiles(s) -> None:
    d_tracks = state.batch_delta("trackCount")
    d_bytes = state.batch_delta("totalLibraryBytes")

    def delta_html(val, fmt) -> str:
        if val is None:
            return ""
        cls = "delta-pos" if val >= 0 else "delta-neg"
        sign = "+" if val >= 0 else "-"
        return f'<span class="delta {cls}">{sign}{fmt(abs(val))} vs last batch</span>'

    hours = s.summary_num("totalPlaybackHours")
    tiles_def = [
        ("Tracks", fmt_int(s.summary_num("trackCount")), delta_html(d_tracks, fmt_int)),
        ("Artists", fmt_int(s.summary_num("artistCount")), ""),
        ("Total Size", fmt_bytes(s.summary_num("totalLibraryBytes")), delta_html(d_bytes, fmt_bytes)),
        ("Genres", fmt_int(s.summary_num("genreCount")), ""),
        ("Total Playback", f"{hours:,.1f} hrs",
         f'<span class="delta">&asymp; {hours / 24:,.1f} days</span>' if hours else ""),
        ("Avg Song Length", fmt_mmss(s.summary_num("avgSongLengthSeconds")), ""),
        ("Median Song Length", fmt_mmss(s.summary_num("medianSongLengthSeconds")), ""),
        ("Avg File Size", fmt_bytes(s.summary_num("avgFileBytes")), ""),
    ]
    tile_html = "".join(
        f'<div class="stat-tile"><div class="label">{label}</div>'
        f'<div class="value">{value}</div>{delta}</div>'
        for label, value, delta in tiles_def
    )
    ui.html(f'<div class="totals">{tile_html}</div>')


def recent_additions(window: str) -> None:
    batches = _windowed_batches(window)[:3]
    if not batches:
        ui.label("No integration batches found.").classes("note")
        return
    rows = []
    for b in batches:
        rows.append(f'<tr class="batch-header"><td colspan="3">Batch {b["date"]} '
                    f'({b["count"]} tracks)</td></tr>')
        tracks = state.batch_tracks(b)[:6]
        for t in tracks:
            title = _esc(str(t.get("title", "?")))
            artist = _esc(str(t.get("primaryArtist", "?")))
            year = _esc(str(t.get("year", "")))
            rows.append(f"<tr><td>{title}</td><td>{artist}</td>"
                        f'<td class="num">{year}</td></tr>')
        more = b["count"] - len(tracks)
        if more > 0:
            rows.append(f'<tr><td colspan="3" style="color:var(--text-dim);font-size:11px;">'
                        f"&hellip; and {more} more</td></tr>")
    ui.html(f'<table class="am-table"><tbody>{"".join(rows)}</tbody></table>')


def _esc(text: str) -> str:
    return text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")

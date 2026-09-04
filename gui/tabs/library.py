"""Library Browser - every row from logs/tracks.json via data_loader.
Table/grid toggle, iTunes-style column picker, search + filter chips,
server-side pagination (only the current page is rendered), and REAL
mutagen-extracted album art in the grid - extracted lazily for the visible
page only and cached in gui/.cache/thumbs.
"""
from __future__ import annotations

from nicegui import ui

from gui import config
from gui.art import get_thumbnail, initials, placeholder_style
from gui.data_loader import fmt_int
from gui.state import state
from gui.theme import CHART_PALETTE


class LibState:
    def __init__(self):
        self.view = "table"
        self.page = 1
        self.search = ""
        self.genre: str | None = None
        self.decade: str | None = None
        self.show_added = False
        self.show_comp = False
        self.show_cover = False
        self.refresh = lambda: None

    @property
    def page_size(self) -> int:
        return config.PAGE_SIZE_GRID if self.view == "grid" else config.PAGE_SIZE_TABLE


L = LibState()


def _genre_color(genre: str) -> str:
    return CHART_PALETTE[hash(genre) % len(CHART_PALETTE)]


def build() -> None:
    with ui.element("header").classes("page"):
        ui.html("<h1>Library</h1>")
        ui.html('<div class="meta">All rows from tracks.json - the GUI parses no XML</div>')

    if state.tracks is None:
        with ui.column().classes("panel w-full").style("align-items:center;padding:48px;gap:12px;"):
            ui.label("No track data yet").style("font-size:16px;font-weight:600;color:var(--text);")
            ui.label(state.load_error or "Run analysis once (Statistics tab) to generate tracks.json.") \
                .classes("note").style("margin:0;")
        return

    @ui.refreshable
    def content():
        _render()

    L.refresh = content.refresh
    content()
    state.on_reload(content.refresh)


def _reset_page_and_refresh() -> None:
    L.page = 1
    L.refresh()


def _render() -> None:
    idx = state.tracks

    # ------- filter chips (genre + decade) -------
    with ui.row().classes("w-full").style("gap:8px;flex-wrap:wrap;margin-bottom:12px;align-items:center;"):
        all_cls = "chip active" if not L.genre and not L.decade else "chip"
        chip = ui.html(f'<div class="{all_cls}">All</div>')
        chip.on("click", lambda _: _clear_filters())
        for g in idx.genres(8):
            cls = "chip active" if L.genre == g else "chip"
            c = ui.html(f'<div class="{cls}">{_esc(g)}</div>')
            c.on("click", lambda _, g=g: _toggle_genre(g))
        for d in idx.decades():
            cls = "chip active" if L.decade == d else "chip"
            c = ui.html(f'<div class="{cls}">{_esc(d)}</div>')
            c.on("click", lambda _, d=d: _toggle_decade(d))

    # ------- toolbar: search, view toggle, column picker -------
    with ui.row().classes("w-full items-center justify-between").style("flex-wrap:wrap;gap:10px;margin-bottom:12px;"):
        ui.input(placeholder="Search title / artist / album...", value=L.search,
                 on_change=lambda e: _set_search(e.value)) \
            .props('dense dark outlined clearable debounce="400"').style("min-width:280px;")
        with ui.row().style("gap:10px;align-items:center;"):
            with ui.element("div").classes("view-toggle").style("display:flex;gap:4px;"):
                bt = ui.html(f'<button class="{"active" if L.view == "table" else ""}">Table</button>')
                bg = ui.html(f'<button class="{"active" if L.view == "grid" else ""}">Grid</button>')
                bt.on("click", lambda _: _set_view("table"))
                bg.on("click", lambda _: _set_view("grid"))
            if L.view == "table":
                with ui.button("Columns").props("outline dense color=grey size=sm icon-right=arrow_drop_down"):
                    with ui.menu().props("dark"):
                        with ui.column().style("padding:10px;gap:4px;background:var(--panel);"):
                            ui.checkbox("Date Added", value=L.show_added,
                                        on_change=lambda e: _set_col("show_added", e.value)).props("dense dark")
                            ui.checkbox("Compilation", value=L.show_comp,
                                        on_change=lambda e: _set_col("show_comp", e.value)).props("dense dark")
                            ui.checkbox("Cover Thumbnail", value=L.show_cover,
                                        on_change=lambda e: _set_col("show_cover", e.value)).props("dense dark")

    rows, total = idx.query(search=L.search, genre=L.genre, decade=L.decade,
                            page=L.page, page_size=L.page_size)

    if total == 0:
        with ui.column().classes("panel w-full").style("align-items:center;padding:40px;gap:8px;"):
            ui.label("No tracks match").style("font-weight:600;color:var(--text);")
            ui.label("Try clearing the search or filter chips.").classes("note").style("margin:0;")
    elif L.view == "grid":
        _grid(rows)
    else:
        _table(rows)

    _pagination(total)


def _clear_filters() -> None:
    L.genre = None
    L.decade = None
    _reset_page_and_refresh()


def _toggle_genre(g: str) -> None:
    L.genre = None if L.genre == g else g
    _reset_page_and_refresh()


def _toggle_decade(d: str) -> None:
    L.decade = None if L.decade == d else d
    _reset_page_and_refresh()


def _set_search(v) -> None:
    L.search = v or ""
    _reset_page_and_refresh()


def _set_view(v: str) -> None:
    L.view = v
    _reset_page_and_refresh()


def _set_col(attr: str, value: bool) -> None:
    setattr(L, attr, bool(value))
    L.refresh()


# ------------------------------------------------------------------ table


def _table(rows: list[dict]) -> None:
    head = []
    if L.show_cover:
        head.append("<th>Cover</th>")
    head += ["<th>Title</th>", "<th>Artist</th>", "<th>Album</th>", "<th>Genre</th>",
             "<th>Track#</th>", "<th>Length</th>", '<th class="num">Year</th>']
    if L.show_added:
        head.append("<th>Added</th>")
    if L.show_comp:
        head.append("<th>Comp.</th>")

    body = []
    for t in rows:
        tds = []
        if L.show_cover:
            thumb = get_thumbnail(t.get("id", ""), t.get("filePath"), t.get("hasArt", False))
            if thumb:
                cover = f'<div class="cover-art sm"><img src="/thumbs/{thumb.name}" alt=""></div>'
            else:
                cover = (f'<div class="cover-art sm" style="{placeholder_style(str(t.get("id", "?")))}">'
                         f'{initials(str(t.get("title", "")), str(t.get("album", "")))}</div>')
            tds.append(f"<td>{cover}</td>")
        tds += [
            f"<td>{_esc(str(t.get('title', '')))}</td>",
            f"<td>{_esc(str(t.get('artists', '')))}</td>",
            f"<td>{_esc(str(t.get('album', '')))}</td>",
            f"<td>{_esc(str(t.get('primaryGenre', '')))}</td>",
            f"<td>{_esc(str(t.get('trackNumber', '')))}</td>",
            f"<td>{_esc(str(t.get('length', '')))}</td>",
            f'<td class="num">{_esc(str(t.get("year", "")))}</td>',
        ]
        if L.show_added:
            tds.append(f"<td>{_esc(str(t.get('addedDate') or '-'))}</td>")
        if L.show_comp:
            tds.append(f"<td>{'Yes' if t.get('compilation') else 'No'}</td>")
        body.append(f"<tr>{''.join(tds)}</tr>")

    ui.html(
        '<table class="am-table"><thead><tr>' + "".join(head) + "</tr></thead>"
        "<tbody>" + "".join(body) + "</tbody></table>"
    ).classes("w-full")


# ------------------------------------------------------------------- grid


def _grid(rows: list[dict]) -> None:
    cards = []
    for t in rows:
        tid = str(t.get("id", "?"))
        title = _esc(str(t.get("title", "")))
        artist = _esc(str(t.get("primaryArtist", "")))
        genre = str(t.get("primaryGenre", "") or "?")
        year = _esc(str(t.get("year", "")))
        # genre + year beats addedDate here: force-regen resets every mirror
        # mtime, so addedDate reads as "everything added today" - noise
        added_html = f"{_esc(genre)} &middot; {year}"

        thumb = get_thumbnail(tid, t.get("filePath"), t.get("hasArt", False))
        badge = "" if t.get("hiResArt") or not t.get("hasArt") else '<span class="lowres-badge">low-res</span>'
        if thumb:
            cover = (f'<div class="cover-art cover-lg" style="position:relative;">'
                     f'<img src="/thumbs/{thumb.name}" loading="lazy" alt="">{badge}</div>')
        else:
            cover = (f'<div class="cover-art cover-lg" style="position:relative;{placeholder_style(tid)}">'
                     f'{initials(str(t.get("title", "")), str(t.get("album", "")))}{badge}</div>')

        cards.append(
            f'<div class="track-card">{cover}'
            f'<div class="status-bar" style="background:{_genre_color(genre)};"></div>'
            f'<div class="info"><div class="t-title" title="{title}">{title}</div>'
            f'<div class="t-artist">{artist}</div>'
            f'<div class="t-meta">{added_html}</div></div></div>'
        )
    ui.html(f'<div class="card-grid">{"".join(cards)}</div>').classes("w-full")


# -------------------------------------------------------------- pagination


def _pagination(total: int) -> None:
    pages = max(1, (total + L.page_size - 1) // L.page_size)
    L.page = min(L.page, pages)

    with ui.row().classes("w-full items-center justify-between").style("margin-top:14px;flex-wrap:wrap;gap:10px;"):
        ui.html(f'<div style="font-size:12px;color:var(--text-dim);">{fmt_int(total)} tracks '
                f"&middot; page {L.page} of {fmt_int(pages)}</div>")

        with ui.row().style("gap:4px;align-items:center;"):
            def page_btn(n: int, label: str | None = None, active: bool = False):
                el = ui.html(f'<span style="padding:4px 9px;border:1px solid '
                             f'{"var(--accent)" if active else "var(--panel-border)"};'
                             f"border-radius:4px;cursor:pointer;font-size:12px;"
                             f'color:var({"--text" if active else "--text-dim"});">{label or n}</span>')
                el.on("click", lambda _, n=n: _go_page(n))

            window = [p for p in range(L.page - 2, L.page + 3) if 1 <= p <= pages]
            if 1 not in window:
                page_btn(1)
                if window and window[0] > 2:
                    ui.html('<span style="color:var(--text-dim);font-size:12px;">&hellip;</span>')
            for p in window:
                page_btn(p, active=(p == L.page))
            if pages not in window:
                if window and window[-1] < pages - 1:
                    ui.html('<span style="color:var(--text-dim);font-size:12px;">&hellip;</span>')
                page_btn(pages)
            if L.page < pages:
                page_btn(L.page + 1, label="Next &rsaquo;")

        with ui.row().style("gap:6px;align-items:center;"):
            ui.html('<span style="font-size:12px;color:var(--text-dim);">Jump to page</span>')
            jump = ui.input(value=str(L.page)).props("dense dark outlined").style("width:70px;")
            ui.button("Go", on_click=lambda: _jump(jump.value, pages)).props("outline dense color=grey size=sm")


def _go_page(n: int) -> None:
    L.page = n
    L.refresh()


def _jump(value, pages: int) -> None:
    try:
        n = int(str(value).strip())
    except ValueError:
        ui.notify("Enter a page number", type="warning")
        return
    L.page = min(max(1, n), pages)
    L.refresh()


def _esc(text: str) -> str:
    return (text.replace("&", "&amp;").replace("<", "&lt;")
            .replace(">", "&gt;").replace('"', "&quot;"))

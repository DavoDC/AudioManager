"""AudioManager GUI - NiceGUI app entry point.

Six tabs (Statistics, Library Browser, Integration, Tag Fix, Mirror,
Services) behind a left nav, per mockup.html. Tabs are built lazily on
first visit and then shown/hidden, so startup stays instant.

Run:  python -m gui.main   (or scripts/launch-gui.bat for the windowless way)
"""
from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from nicegui import app, ui  # noqa: E402

from gui import config, theme  # noqa: E402
from gui.state import state  # noqa: E402

TABS = [
    ("stats", "Statistics", None),
    ("library", "Library Browser", None),
    ("integration", "Integration", None),
    ("tagfix", "Tag Fix", None),
    ("mirror", "Mirror", None),
    ("services", "Services", "G5"),
]

# Serve cached album-art thumbnails (extraction writes only into gui/.cache)
config.THUMBS_DIR.mkdir(parents=True, exist_ok=True)
app.add_static_files("/thumbs", str(config.THUMBS_DIR))

state.load()

# Mood-reactive theme: the library's dominant genre tints the accent system.
# Must run before tab modules import (chart defaults bind colors at import).
if state.stats and state.stats.genre_distribution:
    theme.apply_mood(state.stats.genre_distribution[0]["label"])


def _builder(name: str):
    """Import tab modules lazily so one tab's failure can't kill the app."""
    if name == "stats":
        from gui.tabs.statistics import build
    elif name == "library":
        from gui.tabs.library import build
    elif name == "integration":
        from gui.tabs.integration import build
    elif name == "tagfix":
        from gui.tabs.tagfix import build
    elif name == "mirror":
        from gui.tabs.mirror import build
    else:
        from gui.tabs.services import build
    return build


@ui.page("/")
def index(tab: str = "stats"):
    initial_tab = tab if tab in {t[0] for t in TABS} else "stats"
    ui.add_head_html(theme.HEAD_HTML)
    ui.dark_mode().enable()
    ui.query(".nicegui-content").style("padding:0;gap:0;")

    built: dict[str, ui.element] = {}
    nav_buttons: dict[str, ui.element] = {}

    with ui.row().classes("w-full no-wrap").style("gap:0;align-items:stretch;"):
        with ui.column().classes("am-nav").style("gap:0;"):
            ui.html('<div class="am-brand"><span>Audio</span>Manager</div>')
            for key, label, badge in TABS:
                badge_html = f' <span class="badge">{badge}</span>' if badge else ""
                active = " active" if key == initial_tab else ""
                btn = ui.html(
                    f'<button class="tab-link{active}" id="nav-{key}">{label}{badge_html}</button>'
                )
                nav_buttons[key] = btn
                btn.on("click", lambda _, k=key: switch(k))
            if theme.MOOD_NAME != "Neutral":
                ui.html(
                    '<div style="margin:18px 20px 0;font-size:10px;color:var(--text-dim);'
                    'text-transform:uppercase;letter-spacing:.06em;">Mood</div>'
                    f'<div style="margin:4px 20px 0;font-size:12px;color:var(--accent);'
                    f'font-weight:600;">&#9835; {theme.MOOD_NAME}</div>'
                    '<div style="margin:2px 20px 0;font-size:10px;color:var(--text-dim);'
                    'line-height:1.4;">accent tuned to your library&#39;s dominant genre</div>'
                )

        with ui.column().classes("am-main").style("gap:0;") as main_area:
            pass

    def switch(key: str) -> None:
        for k, el in built.items():
            el.set_visibility(k == key)
        if key not in built:
            with main_area:
                container = ui.column().classes("w-full tab-enter").style("gap:0;")
                built[key] = container
                with container:
                    try:
                        _builder(key)()
                    except Exception as e:  # a broken tab must not kill the shell
                        ui.label(f"This tab failed to build: {e}").classes("note")
        try:
            ui.run_javascript(
                "document.querySelectorAll('.tab-link').forEach(b=>b.classList.remove('active'));"
                f"document.getElementById('nav-{key}')?.classList.add('active');"
                # replay the entrance animation on the now-visible tab container
                "const m=document.querySelector('.am-main');"
                "if(m){[...m.children].forEach(c=>{"
                "  if(getComputedStyle(c).display!=='none'){"
                "    c.classList.remove('tab-enter');void c.offsetWidth;c.classList.add('tab-enter');}"
                "});}"
            )
        except Exception:
            pass  # initial build: client not connected yet; stats starts active via HTML

    switch(initial_tab)


if __name__ in {"__main__", "__mp_main__"}:
    # Under pythonw (windowless launch) stdout/stderr are None and any print
    # crashes the process - route them to a log file instead.
    if sys.stdout is None or sys.stderr is None:
        config.CACHE_DIR.mkdir(parents=True, exist_ok=True)
        _log = open(config.CACHE_DIR / "gui.log", "a", buffering=1, encoding="utf-8")
        sys.stdout = sys.stdout or _log
        sys.stderr = sys.stderr or _log
    import os
    ui.run(
        title="AudioManager",
        host="127.0.0.1",
        port=8471,
        reload=False,
        # AM_GUI_NO_BROWSER=1 suppresses the auto-opened tab (dev restarts)
        show=os.environ.get("AM_GUI_NO_BROWSER") != "1",
        favicon="🎵",
        dark=True,
    )

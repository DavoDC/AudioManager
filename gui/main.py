"""AudioManager GUI - NiceGUI app entry point.

Seven tabs behind a left nav, grouped into two sections by mutate-vs-observe:
"Library Intake" (Acquire, Integration, Tag Fix) and "Library Insight"
(Statistics, Library, Mirror, Services). Tabs are built lazily on first visit
and then shown/hidden, so startup stays instant. See GUI-Architecture.md's
"Design Vision" section for the grouping rationale.

Run:  python -m gui.main   (or scripts/Gui.bat for the windowless way)
Flags: --fixture NAME (demo data, exe disabled), --port N, --no-watch,
--no-browser, --check (query the running instance), --list-fixtures.
Binds 127.0.0.1 only; the browser URL is http://localhost:PORT.
"""
from __future__ import annotations

import argparse
import os
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))


def parse_args(argv=None) -> argparse.Namespace:
    p = argparse.ArgumentParser(prog="python -m gui.main", description=__doc__.split("\n\n")[0])
    p.add_argument("--fixture", "--demo", dest="fixture", default=os.environ.get("AM_GUI_FIXTURE"),
                   help="serve canned demo data (see --list-fixtures); the exe is disabled")
    p.add_argument("--list-fixtures", action="store_true")
    p.add_argument("--port", type=int, default=int(os.environ.get("AM_GUI_PORT", "8471")))
    p.add_argument("--no-watch", action="store_true", help="disable hot reload (default: on)")
    p.add_argument("--no-browser", action="store_true")
    p.add_argument("--check", action="store_true", help="print the running instance's build line and exit")
    return p.parse_known_args(argv)[0]


ARGS = parse_args() if __name__ in {"__main__", "__mp_main__"} else parse_args([])


def _early_exit() -> None:
    """--check and --list-fixtures answer without starting the server."""
    if ARGS.list_fixtures:
        from gui import demo as _d
        for name, desc in _d.SCENARIOS.items():
            print(f"{name:12} {desc}")
        sys.exit(0)
    if ARGS.check:
        import json
        import urllib.request
        try:
            with urllib.request.urlopen(f"http://127.0.0.1:{ARGS.port}/api/build", timeout=3) as r:
                print(json.loads(r.read())["line"])
            sys.exit(0)
        except Exception as e:
            print(f"not running on port {ARGS.port}: {e}")
            sys.exit(1)


if __name__ in {"__main__", "__mp_main__"}:
    _early_exit()

from nicegui import app, ui  # noqa: E402

from gui import api, applog, chrome, config, demo, security, tabdefs, theme  # noqa: E402

if ARGS.fixture:
    demo.apply(ARGS.fixture)

from gui.state import state  # noqa: E402

TABS = tabdefs.TABS
NAV_GROUPS = tabdefs.NAV_GROUPS

# Serve cached album-art thumbnails (extraction writes only into gui/.cache)
config.THUMBS_DIR.mkdir(parents=True, exist_ok=True)
app.add_static_files("/thumbs", str(config.THUMBS_DIR))
# Bundled IBM Plex fonts (no external font hosts; see tests/test_no_external_urls.py)
app.add_static_files("/am-static", str(Path(__file__).resolve().parent / "static"))
api.register(app)
app.add_middleware(security.LoopbackGuard, port_fn=lambda: ARGS.port)

state.load()

# Mood-reactive theme: the library's dominant genre tints the accent system.
# Must run before tab modules import (chart defaults bind colors at import).
if state.stats and state.stats.genre_distribution:
    theme.apply_mood(state.stats.genre_distribution[0]["label"])


def _builder(name: str):
    """Import tab modules lazily so one tab's failure can't kill the app."""
    if name == "stats":
        from gui.tabs.statistics import build
    elif name == "acquire":
        from gui.tabs.acquire import build
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
    ui.add_head_html(chrome.head_html())
    ui.dark_mode().enable()
    ui.query(".nicegui-content").style("padding:0;gap:0;")

    built: dict[str, ui.element] = {}
    nav_buttons: dict[str, ui.element] = {}

    with ui.row().classes("w-full no-wrap").style("gap:0;align-items:stretch;"):
        with ui.column().classes("am-nav").style("gap:0;").props(
                'role=tablist aria-orientation=vertical data-testid=nav'):
            ui.html('<div class="am-brand"><span>Audio</span>Manager</div>')
            tabs_by_key = {key: (label, badge) for key, label, badge in TABS}
            for group_label, keys in NAV_GROUPS:
                ui.html(f'<div class="nav-group-header">{group_label}</div>')
                for key in keys:
                    label, badge = tabs_by_key[key]
                    badge_html = f' <span class="badge">{badge}</span>' if badge else ""
                    active = " active" if key == initial_tab else ""
                    btn = ui.html(
                        f'<button class="tab-link{active}" id="nav-{key}" data-testid="nav-{key}" '
                        f'role="tab" aria-selected="{"true" if key == initial_tab else "false"}" '
                        f'aria-controls="tab-{key}">{label}{badge_html}</button>'
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
                container = ui.column().classes("w-full tab-enter").style("gap:0;") \
                    .props(f'id=tab-{key} role=tabpanel data-testid=tab-{key} aria-labelledby=nav-{key}')
                built[key] = container
                with container:
                    try:
                        _builder(key)()
                    except Exception as e:  # a broken tab must not kill the shell
                        applog.write("tab-build-failed", f"{key}: {type(e).__name__}: {e}")
                        ui.label(f"This tab failed to build: {e}").classes("note") \
                            .props(f'data-testid=tab-error-{key}')
        try:
            ui.run_javascript(
                "document.querySelectorAll('.tab-link').forEach(b=>{"
                "b.classList.remove('active');b.setAttribute('aria-selected','false');});"
                f"document.getElementById('nav-{key}')?.classList.add('active');"
                f"document.getElementById('nav-{key}')?.setAttribute('aria-selected','true');"
                # replay the entrance animation on the now-visible tab container
                "const m=document.querySelector('.am-main');"
                "if(m){[...m.children].forEach(c=>{"
                "  if(getComputedStyle(c).display!=='none'){"
                "    c.classList.remove('tab-enter');void c.offsetWidth;c.classList.add('tab-enter');}"
                "});}"
                # keep the URL in sync so an F5 reload lands back on this tab
                f"history.replaceState(null,'','/?tab={key}');"
            )
        except Exception:
            pass  # initial build: client not connected yet; stats starts active via HTML

    ui.html(chrome.body_html())
    switch(initial_tab)


if __name__ in {"__main__", "__mp_main__"}:
    # Under pythonw (windowless launch) stdout/stderr are None and any print
    # crashes the process - route them to a log file instead.
    if sys.stdout is None or sys.stderr is None:
        config.CACHE_DIR.mkdir(parents=True, exist_ok=True)
        _log = open(config.CACHE_DIR / "gui.log", "a", buffering=1, encoding="utf-8")
        sys.stdout = sys.stdout or _log
        sys.stderr = sys.stderr or _log

    # Hot reload is ON by default (decision D1): touch gui/.cache/reload.trigger
    # and the next poll restarts the process in place; a running job defers the
    # restart (busy guard). --no-watch or AM_GUI_WATCH=0 turns it off.
    _is_hot_reload_restart = False
    _watch = not ARGS.no_watch and os.environ.get("AM_GUI_WATCH", "1") != "0"
    from gui import hot_reload
    _is_hot_reload_restart = os.environ.get(hot_reload.RESTART_ENV_VAR) == "1"
    if _watch:
        from gui.runner import runner as _runner
        hot_reload.BUSY_CHECK = lambda: _runner.busy
        hot_reload.start_watcher(str(config.GUI_ROOT))

    _open = not ARGS.no_browser and os.environ.get("AM_GUI_NO_BROWSER") != "1" and not _is_hot_reload_restart
    _line = f"AudioManager GUI {security.open_url(ARGS.port)} (bound {security.BIND_HOST}) " \
            f"watch={'on' if _watch else 'off'} fixture={demo.fixture_name() or 'none'}"
    print(_line)
    applog.write("start", _line)

    def _open_browser() -> None:
        import webbrowser
        webbrowser.open(security.open_url(ARGS.port))

    if _open:
        app.on_startup(_open_browser)

    ui.run(
        title="AudioManager",
        host=security.BIND_HOST,  # literal loopback constant; the URL we open is a separate value
        port=ARGS.port,
        reload=False,
        show=False,
        favicon="🎵",
        dark=True,
        uvicorn_logging_level="warning",
    )

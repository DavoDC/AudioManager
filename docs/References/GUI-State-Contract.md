# GUI State Contract

What the running GUI tells a program (Claude, a test, a script) about itself. Read-only: nothing here changes the library or runs the exe. Code: `gui/uistate.py` (payloads), `gui/api.py` (routes), `gui/chrome.py` (browser side). UI conventions: `docs/DESIGN.md`.

## Endpoints

All on `127.0.0.1:8471` (reach it as `http://localhost:8471`). Every response has `Cache-Control: no-store` and `nosniff`. A request whose Host is not `127.0.0.1:PORT` or `localhost:PORT` gets 403; a cross-origin POST or websocket gets 403.

- `GET /api/build` gives `{line, sha, dirty, date, boot_id, started_at, fixture}`. `boot_id` changes on every process start; pages poll it and reload themselves.
- `GET /state.json` gives `{schema, generated_at, build, fixture, busy:{busy, action}, tabs:{<key>: <tab state>}}`.
- `GET /state/<tab>.json` gives one tab state. Tab keys: `acquire`, `integration`, `tagfix`, `stats`, `library`, `mirror`, `services`. Unknown key gives 404.
- `GET /api/manifest.json` is the agent manifest: tabs and their URLs, endpoints, every action with its CLI line, the `window.__debug` surface, doc paths and today's log path.
- `POST /__log` relays a browser error into the log. It needs the per-launch token in `X-AM-Token` (the page embeds it); without it the answer is 403.

## Tab state (common fields)

`schema` (integer, bumped on a breaking change, now 1), `tab`, `label`, `generated_at` (local ISO time), `stale` (reserved, currently false), `poll_interval` (null: values change only by user action or a push, so poll `/state` when you need them), `row_count` (true total) and `rows` (at most 500). Every row has a `testid` that is unique within the tab and equals the `data-testid` painted in the page for that row. If a tab's builder throws, the payload still has the common fields plus `error`.

Tab specific fields:

- `stats`: `loaded`, `load_error`, `stats_generated_at`. Rows are the eight summary tiles (`key`, `value`).
- `library`: `loaded`, `total_matching`, `view`, `page`, `page_size`, `search`, `genre`, `decade`. Rows are the current page only (`id`, `title`, `artists`).
- `integration`: `stage` (1 scan, 2 review, 3 and 4 execute), `filter`, `simulated`, `triage`, `accepted_count`, `declined_count`, `exec_done`, `exec_ok`, `exec_summary`, `exec_record_missing`. Rows are scanned entries (`filename`, `title`, `artist`, `destination`, `status`, `accepted`, `new_folder`).
- `acquire`: `track_count`, `extra_count`, `playlist_loaded`, `simulated`, `hide_downloaded`, `fetching` ([current, total] or null), `sync_busy`. Rows are fetched tracks (`key`, `artist`, `title`, `downloaded`, `overridden`) followed by NewMusic-only extras. The painted table is paginated at 50, so painted ids are a subset of state ids.
- `tagfix`: `dry_run_lines`. Rows are the fixed built-in rules (`name`, `status`).
- `mirror`, `services`: `repo` for mirror; no rows.

## Row test id format

`<kind>-<slug>-<4 hex>` from `uistate.testid(kind, key)`: the slug is the key lowercased with runs of non-alphanumerics turned into `-` (48 chars max), the hex is crc32 of the raw key, so two keys that slug alike stay distinct.

## Browser surface

`window.__debug` (version 1): `state()` and `tabState(tab)` return promises of the JSON above, `setTab(key)` clicks the nav button, `forceStatus(text, kind)` shows or clears the status line, `help()` returns this list. A script error or unhandled rejection shows `#error-banner`, logs a console line starting `[app-error]` and is relayed to the log.

## Logs

`gui/.cache/log/YYYY-MM-DD.log` (gitignored) holds start-up lines, `[app-error]` relays, tab build failures and CLI runs. `python -m gui.cli log` tails it.

## CLI parity

Every GUI action has a command: `python -m gui.cli actions` lists them, `python -m gui.cli run <id>` does what the button does (changing actions need `--yes`; `--print` shows the command without running it). `python -m gui.cli state [tab]` prints this contract without a running server.

## Checking it

`python -m gui.ui_check --url http://localhost:8471` compares painted ids with state rows for every tab. `python -m gui.capture` and `python -m gui.verify` are described in `docs/DESIGN.md` ("Fixture mode").

## Command cheat sheet

```
scripts\Gui.bat                                  launch (windowless, hot reload on)
python -m gui.main --check                       is it up? prints the build line
python -m gui.main --fixture review --no-watch   demo data, exe disabled (--list-fixtures)
python -m gui.ui_check --url http://localhost:8471   painted test ids vs /state.json
python -m gui.capture --scenarios normal         headless screenshots to data/captures/
python -m gui.verify                             tests + ui_check + capture, one PASS/FAIL line
```

## Verification ladder

Cheapest rung first, climb only when the rung below cannot answer:

1. A unit test (`python -m pytest gui/tests -q`, seconds).
2. `scripts\deverify.bat --no-pause` or `python -m gui.verify` for the one PASS or FAIL line.
3. DOM text and ids: `/state/<tab>.json`, then `python -m gui.ui_check`.
4. A `capture` PNG, read by eye (`python -m gui.capture`, `data/captures/`).
5. A browser tool, only for real interaction a fixture cannot reproduce; then the log (`python -m gui.cli log`).

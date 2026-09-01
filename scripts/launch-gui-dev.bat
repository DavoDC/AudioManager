@echo off
rem AudioManager GUI launcher - always DEV mode (hot-reload), windowless.
rem Uses pythonw so no console window appears; gui/main.py already redirects
rem stdout/stderr to a log file when pythonw gives it None stdio (see its
rem "Under pythonw" comment). Claude touches gui/.cache/reload.trigger once
rem an edit is fully wired up; the app restarts in place within ~1s and the
rem open browser tab reconnects on its own (NiceGUI's built-in socket.io
rem reconnect) - no manual close/reopen needed. See CLAUDE.md "Dev mode: hot-reload".
rem The GUI opens in the default browser at http://localhost:8471
cd /d "%~dp0.."
set AM_GUI_WATCH=1
start "" pythonw -m gui.main

@echo off
rem AudioManager GUI launcher - always DEV mode (hot-reload), windowless.
rem Uses pythonw so no console window appears; gui/main.py already redirects
rem stdout/stderr to a log file when pythonw gives it None stdio (see its
rem "Under pythonw" comment). Claude touches gui/.cache/reload.trigger once
rem an edit is fully wired up; the app restarts in place within ~1s and the
rem open browser tab reconnects on its own (NiceGUI's built-in socket.io
rem reconnect) - no manual close/reopen needed. See CLAUDE.md "Dev mode: hot-reload".
rem Kills any already-running instance first (double-click while it's still
rem open, or a crashed/orphaned process) - avoids stacking up duplicates that
rem would fight over port 8471. A browser tab only opens on a genuinely fresh
rem start (nothing was running); if an instance was already up, that means a
rem tab is presumably already open, so this relaunch skips opening a new one -
rem reload the existing tab to see the fresh instance.
cd /d "%~dp0.."

set AM_GUI_NO_BROWSER=
for /f %%c in ('powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0kill-gui.ps1"') do set KILLED=%%c
if not "%KILLED%"=="0" set AM_GUI_NO_BROWSER=1

set AM_GUI_WATCH=1
start "" pythonw -m gui.main

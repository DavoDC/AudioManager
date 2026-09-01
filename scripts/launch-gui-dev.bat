@echo off
rem AudioManager GUI launcher - DEV mode with hot-reload. Keeps a console
rem window (so you can see restart/log lines) and starts gui/hot_reload.py's
rem watcher thread. Claude touches gui/.cache/reload.trigger once an edit is
rem fully wired up; the app restarts in place within ~1s and the open browser
rem tab reconnects on its own (NiceGUI's built-in socket.io reconnect) - no
rem manual close/reopen needed. See CLAUDE.md "Dev mode: hot-reload".
rem The GUI opens in the default browser at http://localhost:8471
cd /d "%~dp0.."
set AM_GUI_WATCH=1
python -m gui.main

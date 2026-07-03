@echo off
rem AudioManager GUI launcher - windowless (the whole point is escaping the
rem terminal, so this leaves no console behind). Replaces launch.bat for
rem day-to-day use; launch.bat remains for the CLI menu.
rem The GUI opens in the default browser at http://127.0.0.1:8471
cd /d "%~dp0.."
start "" pythonw -m gui.main

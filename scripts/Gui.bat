@echo off
rem AudioManager GUI launcher - hot reload on by default, windowless, closes at once.
rem The work is done by `pythonw -m gui.main --launch`: it replaces any instance already on the port, runs the preflight
rem (python, imports, config, cache dir, port) and starts the server detached with no inherited handles, then returns once the
rem server answers. pythonw has no console, so a failure shows as a message box and a line in gui\.cache\log\; this window
rem never stays open. A browser tab opens only on a fresh start; if an instance was replaced, reload the existing tab.
rem Usage: double-click, or Gui.bat [--port N] [--no-watch] [--no-browser] [--no-dialog]
rem        python -m gui.main --stop | --status | --check | --preflight   (console verbs, not through this bat)
rem Claude touches gui\.cache\reload.trigger instead of restarting; see CLAUDE.md "Dev mode: hot-reload".
cd /d "%~dp0.."
rem launch breadcrumb: proves the bat ran even if Python never starts
if not exist "gui\.cache" mkdir "gui\.cache"
>> "gui\.cache\launch-breadcrumb.log" echo %date% %time% Gui.bat launch args=[%*]

where pythonw >nul 2>nul || goto nopython
start "" /wait pythonw -m gui.main --launch %*
exit /b %ERRORLEVEL%

:nopython
>> "gui\.cache\launch-breadcrumb.log" echo %date% %time% Gui.bat FAILED pythonw not on PATH
mshta "vbscript:Execute("MsgBox ""pythonw is not on PATH. Install Python 3 and tick Add to PATH, then run Gui.bat again."",16,""AudioManager GUI did not start"":close")"
exit /b 2

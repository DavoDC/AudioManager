@echo off
:: Build + tests with clean exit codes. Claude's one-step verify command.
set EXE=%~dp0..\..\project\AudioManager\bin\Release\AudioManager.exe
set START_TIME=%TIME%

call "%~dp0build.bat" --no-pause
if errorlevel 1 (
    echo [ERROR] Build failed.
    exit /b 1
)

"%EXE%" --test
if errorlevel 1 (
    echo [ERROR] Tests failed.
    exit /b 1
)

echo.
echo ============================================================
echo [VERIFY] OK  Start: %START_TIME%  End: %TIME%
echo ============================================================
exit /b 0

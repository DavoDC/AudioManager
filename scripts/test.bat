@echo off
set EXE=%~dp0..\project\AudioManager\bin\Release\AudioManager.exe
set START_TIME=%TIME%

echo ============================================================
echo AudioManager Tests
echo ============================================================
echo.

call "%~dp0build.bat" --no-pause
if errorlevel 1 (
    echo [ERROR] Build failed. Cannot run tests.
    pause
    cmd /k
)

echo.
"%EXE%" --test

echo.
echo ============================================================
echo [DONE] Start: %START_TIME%  End: %TIME%
echo ============================================================
echo.
cmd /k

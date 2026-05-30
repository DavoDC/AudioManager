@echo off
set START_TIME=%TIME%

call "%~dp0dev\build.bat" --no-pause
if errorlevel 1 (
    echo [ERROR] Build failed.
    if not "%1"=="--no-pause" cmd /k
    exit /b 1
)

"%~dp0..\project\AudioManager\bin\Release\AudioManager.exe"

echo.
echo ============================================================
echo [DONE] Start: %START_TIME%  End: %TIME%
echo ============================================================
echo.
if not "%1"=="--no-pause" cmd /k
exit /b 0

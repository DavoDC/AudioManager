@echo off
set EXE=%~dp0..\..\project\AudioManager\bin\Release\AudioManager.exe
set START_TIME=%TIME%

echo ============================================================
echo AudioManager Tests
echo ============================================================
echo.

call "%~dp0build.bat" --no-pause
if errorlevel 1 (
    echo [ERROR] Build failed. Cannot run tests.
    if not "%1"=="--no-pause" cmd /k
    exit /b 1
)

echo.
"%EXE%" --test
set TEST_EXIT=%ERRORLEVEL%

echo.
echo ============================================================
echo [DONE] Start: %START_TIME%  End: %TIME%
echo ============================================================
echo.
if not "%1"=="--no-pause" cmd /k
exit /b %TEST_EXIT%

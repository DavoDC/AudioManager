@echo off
:: Build + tests. Human mode: no args (window stays open). Claude mode: --no-pause (clean exit).
set EXE=%~dp0..\..\project\AudioManager\bin\Release\AudioManager.exe
set START_TIME=%TIME%

call "%~dp0build.bat" --no-pause
if errorlevel 1 (
    echo [ERROR] Build failed.
    if not "%1"=="--no-pause" cmd /k
    exit /b 1
)

"%EXE%" --test
set TEST_EXIT=%ERRORLEVEL%
if %TEST_EXIT% neq 0 (
    echo [ERROR] Tests failed.
    if not "%1"=="--no-pause" cmd /k
    exit /b 1
)

echo.
echo ============================================================
echo [VERIFY] OK  Start: %START_TIME%  End: %TIME%
echo ============================================================
echo.
if not "%1"=="--no-pause" cmd /k
exit /b 0

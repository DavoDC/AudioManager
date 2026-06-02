@echo off
:: Build + tests + manifest. Human mode: no args (window stays open). Claude mode: --no-pause (clean exit).
set EXE=%~dp0..\..\project\AudioManager\bin\Release\AudioManager.exe
set MANIFEST=%~dp0..\..\test-fixtures\routing-manifest.json
set START_TIME=%TIME%

call "%~dp0build.bat" --no-pause
if errorlevel 1 (
    echo [ERROR] Build failed.
    if not "%1"=="--no-pause" cmd /k
    exit /b 1
)

set TMPUNIT=%TEMP%\am_unit_%RANDOM%.tmp
"%EXE%" --test > "%TMPUNIT%"
set TEST_EXIT=%ERRORLEVEL%
type "%TMPUNIT%"
if %TEST_EXIT% neq 0 (
    del "%TMPUNIT%" 2>nul
    echo [ERROR] Tests failed.
    if not "%1"=="--no-pause" cmd /k
    exit /b 1
)

set TMPMANIFEST=%TEMP%\am_manifest_%RANDOM%.tmp
"%EXE%" --routing-manifest "%MANIFEST%" > "%TMPMANIFEST%"
set MANIFEST_EXIT=%ERRORLEVEL%
type "%TMPMANIFEST%"
if %MANIFEST_EXIT% neq 0 (
    del "%TMPUNIT%" "%TMPMANIFEST%" 2>nul
    echo [ERROR] Manifest tests failed.
    if not "%1"=="--no-pause" cmd /k
    exit /b 1
)

:: Extract and combine test totals  (tokens=2,4 of "Results: X passed, Y failed")
set UNIT_PASS=0 & set UNIT_FAIL=0
set MANIFEST_PASS=0 & set MANIFEST_FAIL=0
for /f "tokens=2,4" %%a in ('findstr "^Results:" "%TMPUNIT%"') do (
    set UNIT_PASS=%%a & set UNIT_FAIL=%%b
)
for /f "tokens=2,4" %%a in ('findstr "^Results:" "%TMPMANIFEST%"') do (
    set MANIFEST_PASS=%%a & set MANIFEST_FAIL=%%b
)
del "%TMPUNIT%" "%TMPMANIFEST%" 2>nul
set /a TOTAL_PASS=%UNIT_PASS%+%MANIFEST_PASS%
set /a TOTAL_FAIL=%UNIT_FAIL%+%MANIFEST_FAIL%

echo.
echo ============================================================
echo [VERIFY] OK  Total: %TOTAL_PASS% passed, %TOTAL_FAIL% failed  Start: %START_TIME%  End: %TIME%
echo ============================================================
echo.
if not "%1"=="--no-pause" cmd /k
exit /b 0

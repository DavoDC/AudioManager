@echo off
:: Build + unit tests + routing manifest. Human mode: no args (window stays open). Claude mode: --no-pause (clean exit).
set EXE=%~dp0..\..\project\AudioManager\bin\Release\AudioManager.exe
set MANIFEST=%~dp0..\..\test-fixtures\routing-manifest.json
set START_TIME=%TIME%

call "%~dp0build.bat" --no-pause
if errorlevel 1 (
    echo [ERROR] Build failed.
    if not "%1"=="--no-pause" cmd /k
    exit /b 1
)

"%EXE%" --verify "%MANIFEST%"
set VERIFY_EXIT=%ERRORLEVEL%

echo.
echo Start: %START_TIME%  End: %TIME%
echo.
if not "%1"=="--no-pause" cmd /k
exit /b %VERIFY_EXIT%

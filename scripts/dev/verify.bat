@echo off
:: Build + unit tests + routing manifest + GUI tests. Human mode: no args (window stays open). Claude mode: --no-pause (clean exit).
set EXE=%~dp0..\..\project\AudioManager\bin\Release\AudioManager.exe
set MANIFEST=%~dp0..\..\test-fixtures\routing-manifest.json
set REPO_ROOT=%~dp0..\..
set START_TIME=%TIME%

call "%~dp0build.bat" --no-pause
if errorlevel 1 (
    echo [ERROR] Build failed.
    if not "%1"=="--no-pause" cmd /k
    exit /b 1
)

echo [step] C# verify
"%EXE%" --verify "%MANIFEST%"
set VERIFY_EXIT=%ERRORLEVEL%

echo [step] GUI tests
pushd "%REPO_ROOT%"
python -m pytest gui\tests\ -q
set PYTEST_EXIT=%ERRORLEVEL%
popd

echo.
echo Start: %START_TIME%  End: %TIME%
echo.
if %VERIFY_EXIT%==0 if %PYTEST_EXIT%==0 (
    echo [PASS] C# verify + GUI tests
    if not "%1"=="--no-pause" cmd /k
    exit /b 0
)
echo [FAIL] C# verify=%VERIFY_EXIT% GUI tests=%PYTEST_EXIT%
if not "%1"=="--no-pause" cmd /k
exit /b 1

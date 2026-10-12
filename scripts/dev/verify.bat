@echo off
:: Build + unit tests + routing manifest + GUI tests. Human mode: no args (window stays open). Claude mode: --no-pause (clean exit).
set TESTEXE=%~dp0..\..\project\AudioManager.Tests\bin\Release\AudioManager.Tests.exe
set MANIFEST=%~dp0..\..\csharp-test-fixtures\routing-manifest.json
set REPO_ROOT=%~dp0..\..
set START_TIME=%TIME%

:: Prerequisites (ARCH-3): named [PREREQ] errors, exit code 3, distinct from a test failure (1).
set VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe
set PREREQ_MSG=
if not exist "%VSWHERE%" set PREREQ_MSG=MSBuild missing: vswhere.exe not found. Install Visual Studio with the .NET desktop workload.
if not defined PREREQ_MSG where python >nul 2>&1 || set PREREQ_MSG=python missing: not on PATH.
if not defined PREREQ_MSG python -c "import pytest" >nul 2>&1 || set PREREQ_MSG=pytest missing: run python -m pip install -r gui\requirements.txt
if defined PREREQ_MSG (
    echo [PREREQ] %PREREQ_MSG%
    echo [PREREQ] This is a missing tool, not a test failure.
    if not "%1"=="--no-pause" cmd /k
    exit /b 3
)

call "%~dp0build.bat" --no-pause
if errorlevel 1 (
    echo [ERROR] Build failed.
    if not "%1"=="--no-pause" cmd /k
    exit /b 1
)
if not exist "%TESTEXE%" (
    echo [PREREQ] Built test exe missing: %TESTEXE%
    echo [PREREQ] The build reported success but produced no test host; check logs\build.log.
    if not "%1"=="--no-pause" cmd /k
    exit /b 3
)

echo [step] C# verify
"%TESTEXE%" --verify "%MANIFEST%"
set VERIFY_EXIT=%ERRORLEVEL%

echo [step] GUI tests + ui_check + capture (python -m gui.verify)
pushd "%REPO_ROOT%"
python -m gui.verify
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

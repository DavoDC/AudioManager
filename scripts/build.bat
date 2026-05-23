@echo off
:: ============================================================
:: AudioManager Build Script
:: ============================================================

:: Locate MSBuild via vswhere (works with any VS 2017+ installation)
for /f "usebackq tokens=*" %%i in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do set MSBUILD="%%i"
if not defined MSBUILD (
    echo [ERROR] MSBuild not found. Install Visual Studio with the .NET workload.
    pause
    exit /b 1
)

set SLN=%~dp0..\project\AudioManager.sln
set EXE=%~dp0..\project\AudioManager\bin\Release\AudioManager.exe
set LOG=%~dp0..\logs\build.log

if not exist "%~dp0..\logs" mkdir "%~dp0..\logs"

echo [BUILD] Compiling AudioManager...
%MSBUILD% "%SLN%" -p:Configuration=Release -p:Platform="Any CPU" -verbosity:minimal > "%LOG%" 2>&1

if errorlevel 1 (
    echo [ERROR] Build failed.
    echo.
    echo Build log:
    type "%LOG%"
    if not "%1"=="--no-pause" pause
    exit /b 1
)

echo [BUILD] Done. Exe: %EXE%
if not "%1"=="--no-pause" pause

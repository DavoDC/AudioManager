@echo off
:: ============================================================
:: AudioManager Build Script
:: ============================================================

setlocal enabledelayedexpansion

set MSBUILD="C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
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
    pause
    exit /b 1
)

echo [BUILD] Done. Exe: %EXE%
pause

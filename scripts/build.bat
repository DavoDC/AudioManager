@echo off
:: ============================================================
:: AudioManager Build Script
:: ============================================================

setlocal enabledelayedexpansion

set MSBUILD="C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
set SLN=%~dp0..\project\AudioManager.sln
set EXE=%~dp0..\project\AudioManager\bin\Release\AudioManager.exe

echo [BUILD] Compiling AudioManager...
%MSBUILD% "%SLN%" -p:Configuration=Release -p:Platform="Any CPU" -verbosity:minimal

if errorlevel 1 (
    echo [ERROR] Build failed.
    exit /b 1
)

echo [BUILD] Done. Exe: %EXE%
exit /b 0

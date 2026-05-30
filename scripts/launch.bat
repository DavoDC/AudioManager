@echo off

:: ============================================================
:: AudioManager Launcher (Interactive Menu)
:: ============================================================

set EXE=%~dp0..\project\AudioManager\bin\Release\AudioManager.exe
set START_TIME=%TIME%

echo ============================================================
echo AudioManager Launcher
echo ============================================================
echo.

:: Build
call "%~dp0dev\build.bat" --no-pause
if errorlevel 1 (
    echo [ERROR] Build failed. Aborting.
    pause
    cmd /k
)

echo.

:: Menu
echo Select mode:
echo   1. Analysis (No Force Regen)
echo   2. Analysis (Force Regen)
echo   3. Integration
echo.
set /p CHOICE=Enter choice (1-3):

if "%CHOICE%"=="1" goto analysis_normal
if "%CHOICE%"=="2" goto analysis_force
if "%CHOICE%"=="3" goto integrate
echo Invalid choice.
pause
cmd /k

:analysis_normal
echo.
echo [RUN] Analysis (No Force Regen)
echo ============================================================
echo.
"%EXE%" analysis
goto done

:analysis_force
echo.
echo [RUN] Analysis (Force Regen)
echo ============================================================
echo.
"%EXE%" analysis --force-regen
goto done

:integrate
echo.
echo [RUN] Integration (Dry Run)
echo ============================================================
echo.
"%EXE%" integrate --dry-run
echo.
echo --- DRY RUN COMPLETE ---
echo.
set /p PROCEED=Proceed with real integration? [y/N]:
if /I not "%PROCEED%"=="y" (
    echo Cancelled.
    goto done
)
echo.
echo [RUN] Integration (Real)
echo ============================================================
echo.
"%EXE%" integrate

:done
echo.
echo ============================================================
echo [DONE] Start: %START_TIME%  End: %TIME%
echo ============================================================
echo.
cmd /k

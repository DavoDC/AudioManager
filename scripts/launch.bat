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
call "%~dp0build.bat"
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
echo   3. Integration (Dry Run)
echo   4. Integration (Real)
echo.
set /p CHOICE=Enter choice (1-4):

if "%CHOICE%"=="1" goto analysis_normal
if "%CHOICE%"=="2" goto analysis_force
if "%CHOICE%"=="3" goto integrate_dry
if "%CHOICE%"=="4" goto integrate_real
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

:integrate_dry
echo.
echo [RUN] Integration (Dry Run)
echo ============================================================
echo.
"%EXE%" integrate --dry-run
goto done

:integrate_real
echo.
echo [WARNING] Real integration will move files. Make sure Dry Run passed first.
set /p CONFIRM=Type YES to confirm:
if /I not "%CONFIRM%"=="YES" (
    echo Cancelled.
    pause
    cmd /k
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

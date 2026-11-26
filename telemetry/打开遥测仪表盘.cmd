@echo off
setlocal
title Yjcm Telemetry Dashboard
cd /d "%~dp0" || goto launch_failed

if not defined WUZHUGELIANG_DASHBOARD_PORT set "WUZHUGELIANG_DASHBOARD_PORT=4173"
set "URL=http://127.0.0.1:%WUZHUGELIANG_DASHBOARD_PORT%"

rem Reuse only the Yjcm service, rather than any website listening on this port.
curl.exe --noproxy "*" --silent --fail --max-time 2 "%URL%/health" 2>nul | findstr.exe /b /c:"yjcm-telemetry-dashboard" >nul
if not errorlevel 1 (
    echo Dashboard is already running at %URL%/
    start "" "%URL%/"
    if errorlevel 1 goto launch_failed
    exit /b 0
)

rem Prefer node from PATH; fall back to the standard Windows installer location.
set "NODE=node.exe"
where /q node.exe
if errorlevel 1 (
    if exist "%ProgramFiles%\nodejs\node.exe" (
        set "NODE=%ProgramFiles%\nodejs\node.exe"
    ) else (
        echo.
        echo Node.js was not found.
        echo Install Node.js 20 or newer, then double-click this file again.
        echo If Node.js is installed in a custom folder, add it to PATH.
        echo.
        pause
        exit /b 1
    )
)

if not exist "dashboard\config.local.json" (
    echo First-time setup is required.
    echo.
    "%NODE%" dashboard\setup.mjs
    if errorlevel 1 (
        echo.
        echo Setup failed. Check the message above and try again.
        pause
        exit /b 1
    )
    echo.
)

echo Starting the local telemetry dashboard...
echo Keep this window open. Press Ctrl+C here to stop it.
echo.
"%NODE%" dashboard\server.mjs --open
if errorlevel 1 goto launch_failed
exit /b 0

:launch_failed
echo.
echo Dashboard startup failed. Check the message above.
pause
exit /b 1

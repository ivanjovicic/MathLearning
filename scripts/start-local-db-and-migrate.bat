@echo off
REM Windows wrapper to run the PowerShell script using Windows PowerShell
SET SCRIPT_DIR=%~dp0
PowerShell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%start-local-db-and-migrate.ps1" %*
IF ERRORLEVEL 1 (
  echo Migration script failed.
  exit /b 1
)
exit /b 0

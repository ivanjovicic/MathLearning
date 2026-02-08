@echo off
REM ============================================
REM MathLearning — Start local DB and run migrations
REM ============================================
SET SCRIPT_DIR=%~dp0
PowerShell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%start-local-db-and-migrate.ps1" %*
IF ERRORLEVEL 1 (
  echo.
  echo Migration script failed.
  pause
  exit /b 1
)
exit /b 0

@echo off
REM start-admin.bat - Start the MathLearning.Admin project locally
SETLOCAL

REM Resolve script directory and project path (relative to repo root)
set "SCRIPT_DIR=%~dp0"
set "PROJECT_PATH=%SCRIPT_DIR%..\src\MathLearning.Admin"

if not exist "%PROJECT_PATH%\" (
  echo Project path not found: %PROJECT_PATH%
  exit /b 1
)

pushd "%PROJECT_PATH%"
echo Starting MathLearning.Admin (Development)...
set "ASPNETCORE_ENVIRONMENT=Development"
set "DOTNET_ENVIRONMENT=Development"

where dotnet >nul 2>&1
if errorlevel 1 (
  echo dotnet CLI not found. Install the .NET SDK: https://dotnet.microsoft.com/
  popd
  exit /b 1
)

dotnet run

popd
ENDLOCAL

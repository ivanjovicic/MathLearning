@echo off
REM ============================================
REM MathLearning — Translation Job Runner
REM ============================================

if "%~1"=="" (
    echo Usage: run-translation-job.bat ^<target-lang^> ^<provider^> [api-key]
    echo.
    echo Examples:
    echo   run-translation-job.bat sr google YOUR_GOOGLE_API_KEY
    echo   run-translation-job.bat de deepl YOUR_DEEPL_API_KEY
    echo.
    echo Or set TRANSLATION_API_KEY environment variable
    pause
    exit /b 1
)

set TARGET_LANG=%1
set PROVIDER=%2
set API_KEY=%3

if "%API_KEY%"=="" (
    if "%TRANSLATION_API_KEY%"=="" (
        echo Error: API key not provided and TRANSLATION_API_KEY not set
        pause
        exit /b 1
    )
    set API_KEY=%TRANSLATION_API_KEY%
)

echo Starting translation job...
echo Target language: %TARGET_LANG%
echo Provider: %PROVIDER%
echo.

dotnet run --project src\MathLearning.TranslationJob\MathLearning.TranslationJob.csproj -- %TARGET_LANG% %PROVIDER% %API_KEY%

if %ERRORLEVEL% neq 0 (
    echo.
    echo Translation job failed!
    pause
    exit /b 1
)

echo.
echo Translation job completed successfully!
pause

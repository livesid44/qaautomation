@echo off
setlocal EnableDelayedExpansion

:: ============================================================
:: QAAutomation - Publish Script
:: Publishes API and Web projects into publish\API and publish\Web,
:: then creates publish.zip containing both folders.
:: ============================================================

set ROOT=%~dp0
set PUBLISH_DIR=%ROOT%publish
set API_OUT=%PUBLISH_DIR%\API
set WEB_OUT=%PUBLISH_DIR%\Web
set ZIP_OUT=%ROOT%publish.zip
set API_PROJ=%ROOT%src\QAAutomation.API\QAAutomation.API.csproj
set WEB_PROJ=%ROOT%src\QAAutomation.Web\QAAutomation.Web.csproj

echo.
echo ============================================================
echo  QAAutomation Publish Script
echo ============================================================
echo.

:: ── Clean previous output ─────────────────────────────────────
if exist "%PUBLISH_DIR%" (
    echo [1/5] Removing previous publish folder...
    rmdir /s /q "%PUBLISH_DIR%"
)
if exist "%ZIP_OUT%" (
    echo       Removing previous publish.zip...
    del /f /q "%ZIP_OUT%"
)

:: ── Publish API ───────────────────────────────────────────────
echo.
echo [2/5] Publishing QAAutomation.API ...
dotnet publish "%API_PROJ%" ^
    --configuration Release ^
    --output "%API_OUT%" ^
    --no-self-contained
if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] API publish failed. Aborting.
    exit /b 1
)
echo       API published to: %API_OUT%

:: ── Publish Web ───────────────────────────────────────────────
echo.
echo [3/5] Publishing QAAutomation.Web ...
dotnet publish "%WEB_PROJ%" ^
    --configuration Release ^
    --output "%WEB_OUT%" ^
    --no-self-contained
if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] Web publish failed. Aborting.
    exit /b 1
)
echo       Web published to: %WEB_OUT%

:: ── Create zip ────────────────────────────────────────────────
echo.
echo [4/5] Creating publish.zip ...
powershell -NoProfile -Command ^
    "Compress-Archive -Path '%PUBLISH_DIR%\*' -DestinationPath '%ZIP_OUT%' -Force"
if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] Failed to create zip. Aborting.
    exit /b 1
)
echo       Archive created: %ZIP_OUT%

:: ── Done ──────────────────────────────────────────────────────
echo.
echo [5/5] Done!
echo.
echo   Publish folder : %PUBLISH_DIR%
echo     API          : %API_OUT%
echo     Web          : %WEB_OUT%
echo   Zip archive    : %ZIP_OUT%
echo.

endlocal

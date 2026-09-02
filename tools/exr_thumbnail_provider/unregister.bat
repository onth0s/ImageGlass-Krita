@echo off
setlocal
cd /d "%~dp0"

echo Unregistering OpenEXR Thumbnail Provider Shell Extension...
regsvr32.exe /u "%~dp0target\release\exr_thumbnail_provider.dll"

if %ERRORLEVEL% EQU 0 (
    echo [SUCCESS] OpenEXR Thumbnail Provider unregistered successfully.
) else (
    echo [ERROR] Failed to unregister OpenEXR Thumbnail Provider (Error: %ERRORLEVEL%). Make sure you run this script as Administrator.
)
pause

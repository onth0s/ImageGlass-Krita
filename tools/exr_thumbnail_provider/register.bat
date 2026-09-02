@echo off
setlocal
cd /d "%~dp0"

echo Registering 64-bit OpenEXR Thumbnail Provider Shell Extension...
regsvr32.exe "%~dp0target\release\exr_thumbnail_provider.dll"

if %ERRORLEVEL% EQU 0 (
    echo [SUCCESS] OpenEXR Thumbnail Provider registered successfully.
) else (
    echo [ERROR] Failed to register OpenEXR Thumbnail Provider (Error: %ERRORLEVEL%). Make sure you run this script as Administrator.
)
pause

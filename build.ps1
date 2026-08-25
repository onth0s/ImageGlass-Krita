# Kill any running instances of ImageGlass to release file locks
Get-Process -Name "ImageGlass" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

Write-Host "Building ImageGlass Release (x64)..." -ForegroundColor Cyan

$projectPath = "ImageGlass/source/ImageGlass.Win32/ImageGlass.Win32.csproj"

dotnet build $projectPath -p:Platform=x64 -c Release

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nBuild Succeeded!" -ForegroundColor Green
} else {
    Write-Host "`nBuild Failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}

Read-Host -Prompt "Press Enter to exit"

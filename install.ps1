# LUSharp Installer for Windows
# Usage: irm https://raw.githubusercontent.com/LUSharp/LUSharp/master/install.ps1 | iex

$ErrorActionPreference = "Stop"

$repo = "LUSharp/LUSharp"
$installDir = "$env:LOCALAPPDATA\LUSharp"
$asset = "lusharp-win-x64.zip"

Write-Host "Installing LUSharp..." -ForegroundColor Cyan

# Get latest release info
try {
    $release = Invoke-RestMethod "https://api.github.com/repos/$repo/releases/latest"
} catch {
    Write-Host "ERROR: Failed to fetch latest release from GitHub." -ForegroundColor Red
    Write-Host "  $_" -ForegroundColor Red
    exit 1
}

$tag = $release.tag_name
$downloadUrl = ($release.assets | Where-Object { $_.name -eq $asset }).browser_download_url

if (-not $downloadUrl) {
    Write-Host "ERROR: Asset '$asset' not found in release $tag." -ForegroundColor Red
    Write-Host "Available assets:" -ForegroundColor Yellow
    $release.assets | ForEach-Object { Write-Host "  $($_.name)" }
    exit 1
}

Write-Host "  Version: $tag"
Write-Host "  Downloading $asset..."

# Download to temp
$tempFile = Join-Path $env:TEMP "lusharp-install.zip"
Invoke-WebRequest -Uri $downloadUrl -OutFile $tempFile -UseBasicParsing

# Extract
if (Test-Path $installDir) {
    Remove-Item $installDir -Recurse -Force
}
Expand-Archive -Path $tempFile -DestinationPath $installDir -Force
Remove-Item $tempFile -Force

# Add to PATH if not already present
$userPath = [Environment]::GetEnvironmentVariable("PATH", "User")
if ($userPath -notlike "*$installDir*") {
    [Environment]::SetEnvironmentVariable("PATH", "$userPath;$installDir", "User")
    Write-Host "  Added $installDir to User PATH." -ForegroundColor Green
    Write-Host "  Restart your terminal for PATH changes to take effect." -ForegroundColor Yellow
} else {
    Write-Host "  $installDir already in PATH." -ForegroundColor Green
}

# Verify
$exe = Join-Path $installDir "lusharp.exe"
if (Test-Path $exe) {
    $version = & $exe --version 2>&1
    Write-Host "`nLUSharp $tag installed successfully!" -ForegroundColor Green
    Write-Host "  Location: $installDir"
    Write-Host "  $version"
    Write-Host "`nGet started:"
    Write-Host "  lusharp new MyGame"
} else {
    Write-Host "WARNING: lusharp.exe not found at $exe" -ForegroundColor Yellow
    Write-Host "The archive may have a nested folder. Check $installDir" -ForegroundColor Yellow
}

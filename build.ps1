#!/usr/bin/env pwsh
# Build script for LiveMap mod

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$Clean,
    [switch]$SkipWeb
)

$ErrorActionPreference = "Stop"

$ProjectRoot = $PSScriptRoot
$ProjectFile = Join-Path $ProjectRoot "LiveMap.csproj"

# Colors for output
function Write-Step($message) { Write-Host "==> $message" -ForegroundColor Cyan }
function Write-Success($message) { Write-Host "✓ $message" -ForegroundColor Green }
function Write-Error($message) { Write-Host "✗ $message" -ForegroundColor Red }

Write-Host ""
Write-Host "LiveMap Build Script" -ForegroundColor Magenta
Write-Host "====================" -ForegroundColor Magenta
Write-Host ""

# Check for VINTAGE_STORY environment variable
if (-not $env:VINTAGE_STORY) {
    Write-Error "VINTAGE_STORY environment variable is not set."
    Write-Host "Please set it to your Vintage Story installation directory."
    Write-Host "Example: `$env:VINTAGE_STORY = 'C:\Program Files\Vintagestory'"
    exit 1
}

if (-not (Test-Path $env:VINTAGE_STORY)) {
    Write-Error "VINTAGE_STORY path does not exist: $env:VINTAGE_STORY"
    exit 1
}

Write-Success "VINTAGE_STORY: $env:VINTAGE_STORY"

# Clean if requested
if ($Clean) {
    Write-Step "Cleaning build artifacts..."
    dotnet clean $ProjectFile -c $Configuration --nologo -v q
    if (Test-Path (Join-Path $ProjectRoot "web/dist")) {
        Remove-Item -Recurse -Force (Join-Path $ProjectRoot "web/dist")
    }
    Write-Success "Clean complete"
}

# Build web frontend
if (-not $SkipWeb) {
    Write-Step "Building web frontend..."
    Push-Location (Join-Path $ProjectRoot "web")
    try {
        npm install --silent
        if ($LASTEXITCODE -ne 0) { throw "npm install failed" }
        npm run build
        if ($LASTEXITCODE -ne 0) { throw "npm run build failed" }
        Write-Success "Web frontend built"
    }
    finally {
        Pop-Location
    }
}

# Build the mod
Write-Step "Building mod ($Configuration)..."
dotnet build $ProjectFile -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    exit 1
}
Write-Success "Build complete"

# Find the output zip
$BinDir = Join-Path $ProjectRoot "bin"
$ZipFiles = Get-ChildItem -Path $BinDir -Filter "*.zip" | Sort-Object LastWriteTime -Descending
if ($ZipFiles.Count -gt 0) {
    $LatestZip = $ZipFiles[0]
    Write-Host ""
    Write-Host "Package created:" -ForegroundColor Green
    Write-Host "  $($LatestZip.FullName)" -ForegroundColor Yellow
    Write-Host "  Size: $([math]::Round($LatestZip.Length / 1KB, 2)) KB" -ForegroundColor Gray
}

# Move the zip out into a Release folder
$ReleaseDir = Join-Path $ProjectRoot "Release"
if (Test-Path $ReleaseDir) {
    Remove-Item -Path "$ReleaseDir\*" -Force -ErrorAction SilentlyContinue
}
else {
    New-Item -ItemType Directory -Path $ReleaseDir | Out-Null
}
Move-Item -Path $LatestZip.FullName -Destination (Join-Path $ReleaseDir $LatestZip.Name)

Write-Host ""
Write-Success "Build completed successfully!"

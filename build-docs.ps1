#!/usr/bin/env pwsh
# Script to build Doxygen documentation for LiveMap

$ErrorActionPreference = "Stop"

$ProjectRoot = $PSScriptRoot
$DoxygenConf = Join-Path $ProjectRoot "doxygen.conf"
$DocsDir = Join-Path $ProjectRoot "docs"

# Colors for output
function Write-Step($message) { Write-Host "==> $message" -ForegroundColor Cyan }
function Write-Success($message) { Write-Host "✓ $message" -ForegroundColor Green }
function Write-Error-Msg($message) { Write-Host "✗ $message" -ForegroundColor Red }

Write-Host ""
Write-Host "LiveMap Documentation Build" -ForegroundColor Magenta
Write-Host "===========================" -ForegroundColor Magenta
Write-Host ""

# Check if doxygen is in PATH
if (-not (Get-Command doxygen -ErrorAction SilentlyContinue)) {
	Write-Error-Msg "Doxygen is not found in your PATH."
	Write-Host "If you just installed it, you may need to restart your terminal."
	Write-Host "You can download it from: https://www.doxygen.nl/download.html"
	exit 1
}

Write-Step "Running Doxygen..."
doxygen $DoxygenConf

if ($LASTEXITCODE -eq 0) {
	Write-Success "Documentation build successful!"
	$IndexFile = Join-Path $DocsDir "html\index.html"
	if (Test-Path $IndexFile) {
		Write-Host ""
		Write-Host "You can view the documentation at:" -ForegroundColor Green
		Write-Host "  $IndexFile" -ForegroundColor Yellow
	}
}
else {
	Write-Error-Msg "Doxygen failed with exit code $LASTEXITCODE"
	exit $LASTEXITCODE
}

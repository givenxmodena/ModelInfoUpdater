# Build-Release.ps1
# PowerShell script to build and package ModelInfoUpdater releases
# 
# Prerequisites:
# 1. Install Velopack CLI: dotnet tool install -g vpk
# 2. Have Inno Setup installed for creating the initial installer
#
# Usage:
#   .\Build-Release.ps1 -Version "1.0.1"
#   .\Build-Release.ps1 -Version "1.0.1" -PublishToGitHub
#
param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    
    [switch]$PublishToGitHub,
    
    [string]$GitHubToken = $env:GITHUB_TOKEN
)

$ErrorActionPreference = "Stop"

# Configuration
$RepoRoot = Split-Path -Parent $PSScriptRoot
$SrcDir = Join-Path $RepoRoot "src"
$BuildOutputDir = Join-Path $RepoRoot "build-output"
$ReleasesDir = Join-Path $RepoRoot "releases"
$AppId = "ModelInfoUpdater"
$GitHubRepo = "givenxmodena/ModelInfoUpdater"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "ModelInfoUpdater Release Builder v$Version" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Step 1: Clean and build
Write-Host "`n[1/5] Building solution..." -ForegroundColor Yellow

Push-Location $SrcDir
try {
    dotnet clean ModelInfoUpdater.csproj --configuration Release
    if ($LASTEXITCODE -ne 0) { throw "Clean failed" }
    
    dotnet build ModelInfoUpdater.csproj --configuration Release
    if ($LASTEXITCODE -ne 0) { throw "Build failed for main project" }
    
    dotnet build Updater/ModelInfoUpdater.Updater.csproj --configuration Release
    if ($LASTEXITCODE -ne 0) { throw "Build failed for Updater project" }
}
finally {
    Pop-Location
}

Write-Host "Build completed successfully!" -ForegroundColor Green

# Step 2: Create releases directory
Write-Host "`n[2/5] Preparing release directory..." -ForegroundColor Yellow

if (-not (Test-Path $ReleasesDir)) {
    New-Item -ItemType Directory -Path $ReleasesDir | Out-Null
}

# Step 3: Package with Velopack for .NET 8.0 (Revit 2026)
Write-Host "`n[3/5] Packaging with Velopack (net8.0-windows)..." -ForegroundColor Yellow

$Net8OutputDir = Join-Path $BuildOutputDir "net8.0-windows"

# Check if vpk is installed
$vpkPath = Get-Command vpk -ErrorAction SilentlyContinue
if (-not $vpkPath) {
    Write-Host "Installing Velopack CLI..." -ForegroundColor Yellow
    dotnet tool install -g vpk
}

# Create Velopack package
# The updater exe is the main executable that Velopack manages
# It contains the add-in DLLs in framework-specific subfolders (net48/, net8.0-windows/)
vpk pack `
    --packId $AppId `
    --packVersion $Version `
    --packDir $Net8OutputDir `
    --mainExe "ModelInfoUpdater.Updater.exe" `
    --outputDir $ReleasesDir `
    --framework "net8.0-x64-desktop"

if ($LASTEXITCODE -ne 0) {
    throw "Velopack packaging failed. Check that ModelInfoUpdater.Updater.exe exists in $Net8OutputDir"
}

# Step 4: Create ZIP packages for manual distribution
Write-Host "`n[4/5] Creating ZIP packages..." -ForegroundColor Yellow

$Net48OutputDir = Join-Path $BuildOutputDir "net48"

# Package for Revit 2026
$Zip2026 = Join-Path $ReleasesDir "ModelInfoUpdater-$Version-Revit2026.zip"
Compress-Archive -Path @(
    (Join-Path $Net8OutputDir "ModelInfoUpdater.dll"),
    (Join-Path $Net8OutputDir "ModelInfoUpdater.Updater.exe"),
    (Join-Path $Net8OutputDir "Velopack.dll"),
    (Join-Path $RepoRoot "ModelInfoUpdater.addin")
) -DestinationPath $Zip2026 -Force

Write-Host "Created: $Zip2026" -ForegroundColor Green

# Package for Revit 2024
$Zip2024 = Join-Path $ReleasesDir "ModelInfoUpdater-$Version-Revit2024.zip"
Compress-Archive -Path @(
    (Join-Path $Net48OutputDir "ModelInfoUpdater.dll"),
    (Join-Path $Net48OutputDir "Velopack.dll"),
    (Join-Path $RepoRoot "ModelInfoUpdater.addin")
) -DestinationPath $Zip2024 -Force

Write-Host "Created: $Zip2024" -ForegroundColor Green

# Step 5: Publish to GitHub (optional)
if ($PublishToGitHub) {
    Write-Host "`n[5/5] Publishing to GitHub Releases..." -ForegroundColor Yellow

    if (-not $GitHubToken) {
        throw "GitHub token required. Set GITHUB_TOKEN environment variable or use -GitHubToken parameter."
    }

    # Collect Velopack artifacts
    $FullNupkg = Join-Path $ReleasesDir "ModelInfoUpdater-$Version-full.nupkg"
    $DeltaNupkg = Join-Path $ReleasesDir "ModelInfoUpdater-$Version-delta.nupkg"
    $ReleasesFile = Join-Path $ReleasesDir "RELEASES"

    $artifactsToUpload = @($Zip2026, $Zip2024)

    # Add Velopack packages if they exist
    if (Test-Path $FullNupkg) { $artifactsToUpload += $FullNupkg }
    if (Test-Path $DeltaNupkg) { $artifactsToUpload += $DeltaNupkg }
    if (Test-Path $ReleasesFile) { $artifactsToUpload += $ReleasesFile }

    # Create release using GitHub CLI (gh) if available
    $ghPath = Get-Command gh -ErrorAction SilentlyContinue
    if ($ghPath) {
        gh release create "v$Version" `
            $artifactsToUpload `
            --repo $GitHubRepo `
            --title "v$Version" `
            --notes "Release v$Version of Model Info Updater"

        Write-Host "Published to GitHub!" -ForegroundColor Green
    }
    else {
        Write-Warning "GitHub CLI (gh) not found. Please install it or upload releases manually."
        Write-Host "Files ready for upload in: $ReleasesDir" -ForegroundColor Yellow
    }
}
else {
    Write-Host "`n[5/5] Skipping GitHub publish (use -PublishToGitHub to enable)" -ForegroundColor Gray
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Release $Version build complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan

Write-Host "`nCreated artifacts:" -ForegroundColor Yellow

# List Velopack packages
$FullNupkg = Join-Path $ReleasesDir "ModelInfoUpdater-$Version-full.nupkg"
$DeltaNupkg = Join-Path $ReleasesDir "ModelInfoUpdater-$Version-delta.nupkg"
$ReleasesFile = Join-Path $ReleasesDir "RELEASES"

if (Test-Path $FullNupkg) {
    Write-Host "  ✓ $FullNupkg" -ForegroundColor Green
} else {
    Write-Host "  ✗ $FullNupkg (missing)" -ForegroundColor Red
}

if (Test-Path $DeltaNupkg) {
    Write-Host "  ✓ $DeltaNupkg" -ForegroundColor Green
} else {
    Write-Host "  ℹ $DeltaNupkg (not created - expected for first release)" -ForegroundColor Gray
}

if (Test-Path $ReleasesFile) {
    Write-Host "  ✓ $ReleasesFile" -ForegroundColor Green
} else {
    Write-Host "  ✗ $ReleasesFile (missing)" -ForegroundColor Red
}

# List convenience ZIPs
if (Test-Path $Zip2026) {
    Write-Host "  ✓ $Zip2026" -ForegroundColor Green
} else {
    Write-Host "  ✗ $Zip2026 (missing)" -ForegroundColor Red
}

if (Test-Path $Zip2024) {
    Write-Host "  ✓ $Zip2024" -ForegroundColor Green
} else {
    Write-Host "  ✗ $Zip2024 (missing)" -ForegroundColor Red
}

Write-Host "`nOutput directory: $ReleasesDir" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan


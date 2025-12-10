<#
.SYNOPSIS
    Creates a GitHub release and uploads Velopack packages for Model Info Updater.

.DESCRIPTION
    This script automates the release process by:
    1. Reading the GitHub token from Windows Credential Manager
    2. Creating a new GitHub release
    3. Uploading all release assets (.nupkg, .exe, RELEASES file)

.PARAMETER Version
    The version number for the release (e.g., "1.4.0")

.PARAMETER Draft
    If specified, creates a draft release instead of publishing immediately

.PARAMETER Prerelease
    If specified, marks the release as a prerelease

.EXAMPLE
    .\Create-GitHubRelease.ps1 -Version "1.4.0"

.EXAMPLE
    .\Create-GitHubRelease.ps1 -Version "1.5.0-beta" -Prerelease

.NOTES
    ============================================================
    TOKEN SETUP (First-time setup)
    ============================================================
    
    The GitHub token is stored securely in Windows Credential Manager.
    
    To SET UP the token (run once):
        cmdkey /generic:github-pat-modelinfoupdater /user:YOUR_GITHUB_USERNAME /pass:YOUR_TOKEN
    
    To VIEW stored credentials:
        cmdkey /list:github-pat-modelinfoupdater
    
    To UPDATE/ROTATE the token:
        cmdkey /delete:github-pat-modelinfoupdater
        cmdkey /generic:github-pat-modelinfoupdater /user:YOUR_GITHUB_USERNAME /pass:NEW_TOKEN
    
    To DELETE the token:
        cmdkey /delete:github-pat-modelinfoupdater
    
    ============================================================
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    
    [switch]$Draft,
    
    [switch]$Prerelease
)

$ErrorActionPreference = "Stop"

# ============================================================
# CONFIGURATION
# ============================================================
$Owner = "givenxmodena"          # GitHub organization/user
$Repo = "ModelInfoUpdater"       # Repository name
$CredentialName = "github-pat-modelinfoupdater"  # Windows Credential Manager entry name

$RepoRoot = Split-Path -Parent $PSScriptRoot
$ReleasesDir = Join-Path $RepoRoot "releases"

# ============================================================
# RETRIEVE TOKEN FROM WINDOWS CREDENTIAL MANAGER
# ============================================================
function Get-GitHubToken {
    Write-Host "Retrieving GitHub token from Windows Credential Manager..." -ForegroundColor Cyan
    
    # Use PowerShell to read from Credential Manager via .NET
    Add-Type -AssemblyName System.Runtime.InteropServices
    
    $sig = @"
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credentialPtr);
    
    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool CredFree(IntPtr cred);
"@
    
    # Alternative: Use cmdkey to verify existence, then use CredentialManager module or vault
    $credCheck = cmdkey /list:$CredentialName 2>&1
    if ($credCheck -match "not found" -or $LASTEXITCODE -ne 0) {
        throw @"

ERROR: GitHub token not found in Windows Credential Manager!

To set up the token, run:
    cmdkey /generic:$CredentialName /user:YOUR_GITHUB_USERNAME /pass:YOUR_GITHUB_TOKEN

Get a token from: https://github.com/settings/tokens
Required scope: 'repo' (for private repositories)
"@
    }
    
    # Use PowerShell CredentialManager module if available, otherwise fallback to environment
    try {
        # Try to use the CredentialManager module
        if (Get-Module -ListAvailable -Name CredentialManager) {
            Import-Module CredentialManager
            $cred = Get-StoredCredential -Target $CredentialName
            if ($cred) {
                return $cred.GetNetworkCredential().Password
            }
        }
    } catch {
        # Module not available, continue to fallback
    }
    
    # Fallback: Check environment variable
    if ($env:GITHUB_TOKEN) {
        Write-Host "Using GITHUB_TOKEN environment variable" -ForegroundColor Yellow
        return $env:GITHUB_TOKEN
    }
    
    throw @"

ERROR: Could not retrieve token automatically.

Options:
1. Install CredentialManager module: Install-Module -Name CredentialManager -Scope CurrentUser
2. Set GITHUB_TOKEN environment variable: `$env:GITHUB_TOKEN = 'your_token'
3. The token IS stored in Credential Manager - you may need to retrieve it manually

To retrieve manually and set as env var:
    # Open Credential Manager in Control Panel > User Accounts > Credential Manager
    # Find 'github-pat-modelinfoupdater' under Windows Credentials
    # Copy the password and set: `$env:GITHUB_TOKEN = 'copied_token'
"@
}

# ============================================================
# GITHUB API FUNCTIONS
# ============================================================
function Invoke-GitHubApi {
    param(
        [string]$Endpoint,
        [string]$Method = "GET",
        [object]$Body,
        [string]$ContentType = "application/json"
    )
    
    $headers = @{
        "Authorization" = "Bearer $script:GitHubToken"
        "Accept" = "application/vnd.github+json"
        "X-GitHub-Api-Version" = "2022-11-28"
    }
    
    $params = @{
        Uri = "https://api.github.com$Endpoint"
        Method = $Method
        Headers = $headers
        ContentType = $ContentType
    }
    
    if ($Body) {
        if ($ContentType -eq "application/json") {
            $params.Body = $Body | ConvertTo-Json -Depth 10
        } else {
            $params.Body = $Body
        }
    }
    
    Invoke-RestMethod @params
}

function Upload-ReleaseAsset {
    param(
        [string]$UploadUrl,
        [string]$FilePath,
        [string]$FileName
    )

    $headers = @{
        "Authorization" = "Bearer $script:GitHubToken"
        "Accept" = "application/vnd.github+json"
        "X-GitHub-Api-Version" = "2022-11-28"
        "Content-Type" = "application/octet-stream"
    }

    $uploadUri = $UploadUrl -replace '\{\?name,label\}', "?name=$FileName"
    $fileBytes = [System.IO.File]::ReadAllBytes($FilePath)

    Invoke-RestMethod -Uri $uploadUri -Method Post -Headers $headers -Body $fileBytes
}

# ============================================================
# MAIN EXECUTION
# ============================================================
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "GitHub Release Creator v$Version" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Step 1: Get token
$script:GitHubToken = Get-GitHubToken
Write-Host "✓ Token retrieved successfully" -ForegroundColor Green

# Step 2: Verify release assets exist
Write-Host "`nVerifying release assets..." -ForegroundColor Yellow

$assets = @(
    @{ Name = "ModelInfoUpdater-$Version-full.nupkg"; Required = $true },
    @{ Name = "ModelInfoUpdater-$Version-delta.nupkg"; Required = $false },
    @{ Name = "ModelInfoUpdater-win-Setup.exe"; Required = $true },
    @{ Name = "RELEASES"; Required = $true }
)

$filesToUpload = @()
foreach ($asset in $assets) {
    $filePath = Join-Path $ReleasesDir $asset.Name
    if (Test-Path $filePath) {
        Write-Host "  ✓ Found: $($asset.Name)" -ForegroundColor Green
        $filesToUpload += @{ Path = $filePath; Name = $asset.Name }
    } elseif ($asset.Required) {
        throw "Required file not found: $filePath"
    } else {
        Write-Host "  - Skipping (not found): $($asset.Name)" -ForegroundColor Gray
    }
}

# Step 3: Create release
Write-Host "`nCreating GitHub release v$Version..." -ForegroundColor Yellow

$releaseNotes = @"
## Model Info Updater v$Version

### What's New
- Seamless auto-restart updates - Revit closes and restarts automatically after update
- Smart detection of unsaved work - prompts to save before updating

### How It Works
1. When an update is available, click "Update Now"
2. The update downloads in the background
3. Revit closes automatically (if no unsaved work)
4. Revit restarts with the new version loaded

### Installation
**New Users:** Download and run ``ModelInfoUpdater-win-Setup.exe``

**Existing Users:** The add-in will automatically detect this update when you open Revit.

### Supported Revit Versions
- Revit 2024, 2025 (.NET Framework 4.8)
- Revit 2026 (.NET 8.0)
"@

$releaseBody = @{
    tag_name = "v$Version"
    name = "Model Info Updater v$Version"
    body = $releaseNotes
    draft = $Draft.IsPresent
    prerelease = $Prerelease.IsPresent
}

try {
    $release = Invoke-GitHubApi -Endpoint "/repos/$Owner/$Repo/releases" -Method "POST" -Body $releaseBody
    Write-Host "✓ Release created: $($release.html_url)" -ForegroundColor Green
} catch {
    if ($_.Exception.Response.StatusCode -eq 422) {
        Write-Host "Release v$Version may already exist. Checking..." -ForegroundColor Yellow
        $releases = Invoke-GitHubApi -Endpoint "/repos/$Owner/$Repo/releases"
        $release = $releases | Where-Object { $_.tag_name -eq "v$Version" }
        if ($release) {
            Write-Host "Found existing release. Will upload assets to it." -ForegroundColor Yellow
        } else {
            throw $_
        }
    } else {
        throw $_
    }
}

# Step 4: Upload assets
Write-Host "`nUploading release assets..." -ForegroundColor Yellow

foreach ($file in $filesToUpload) {
    Write-Host "  Uploading $($file.Name)..." -ForegroundColor Gray
    try {
        Upload-ReleaseAsset -UploadUrl $release.upload_url -FilePath $file.Path -FileName $file.Name
        Write-Host "  ✓ Uploaded: $($file.Name)" -ForegroundColor Green
    } catch {
        Write-Host "  ✗ Failed: $($file.Name) - $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Done!
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Release complete!" -ForegroundColor Green
Write-Host "URL: $($release.html_url)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan


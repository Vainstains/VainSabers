#Requires -Version 5.1
<#
.SYNOPSIS
    Build VainSabers for multiple Beat Saber versions.

.DESCRIPTION
    Calls `dotnet publish` once per GameVersion, passing both GameVersion and
    BeatSaberDir as MSBuild properties so you do NOT need to edit
    VainSabers.csproj.user between builds.

    - GameVersion controls the `GameVersion` field in the generated BSIPA
      manifest (Directory.Build.props uses Condition="'$(GameVersion)'=='')"
      so you can override it per-invocation).
    - BeatSaberDir must point at the root Beat Saber folder for that version
      (contains Beat Saber_Data/Managed and Plugins).

.PARAMETER Versions
    List of game versions to build. Defaults to the 5 requested versions.

.PARAMETER BSInstancesDir
    Folder that contains one sub-folder per version (BSManager default).
    E.g. C:\Users\dbasp\BSManager\BSInstances\1.40.8

.PARAMETER Configuration
    Release or Debug.

.PARAMETER OutputRoot
    Where to copy versioned zips. Defaults to ./ReleaseVers.

.EXAMPLE
    .\BuildAll.ps1
    # builds 1.29.1, 1.34.2, 1.37.1, 1.40.8, 1.42.1 using BSManager layout

.EXAMPLE
    .\BuildAll.ps1 -Versions @("1.40.8") -Configuration Debug -BSInstancesDir "D:\BeatSaber\Vanilla"

.EXAMPLE
    # single version without the script:
    dotnet publish VainSabers -c Release -p:GameVersion=1.29.1 -p:BeatSaberDir="C:/Users/dbasp/BSManager/BSInstances/1.29.1"
#>
[CmdletBinding()]
param(
    [string[]]$Versions = @("1.29.1", "1.34.2", "1.37.1", "1.40.8", "1.42.1"),
    [string]$BSInstancesDir = "C:/Users/dbasp/BSManager/BSInstances",
    [ValidateSet("Release","Debug")][string]$Configuration = "Release",
    [string]$OutputRoot = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = $PSScriptRoot
$proj = Join-Path $repoRoot "VainSabers\VainSabers.csproj"

if (-not (Test-Path $proj)) {
    Write-Error "Could not find $proj"
    exit 1
}

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot "ReleaseVers"
}
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

# Backup existing csproj.user so we can restore after multi-version loop
$csprojUser = Join-Path $repoRoot "VainSabers\VainSabers.csproj.user"
$backupUser = $null
$hadBackup = $false
if (Test-Path $csprojUser) {
    $backupUser = Get-Content $csprojUser -Raw
    $hadBackup = $true
}

try {
foreach ($ver in $Versions) {
    $bsDir = Join-Path $BSInstancesDir $ver

    # Allow BSInstancesDir to be a single game dir (no version subfolder)
    if ($Versions.Count -eq 1 -and (Test-Path (Join-Path $BSInstancesDir "Beat Saber_Data"))) {
        $bsDir = $BSInstancesDir
    }

    if (-not (Test-Path $bsDir)) {
        Write-Warning "[$ver] BeatSaberDir not found: $bsDir - skipping (set -BSInstancesDir or install that version via BSManager/Steam)."
        continue
    }
    if (-not (Test-Path (Join-Path $bsDir "Beat Saber_Data\Managed\Main.dll"))) {
        Write-Warning "[$ver] Main.dll not found under $bsDir - is this a valid Beat Saber install? Skipping."
        continue
    }

    Write-Host "`n=== Building $ver ($Configuration) against $bsDir ===" -ForegroundColor Cyan

    # Write a per-version csproj.user so BeatSaberDir is authoritative even if
    # the user's original file is unconditional (dotnet -p:BeatSaberDir may be
    # ignored when csproj.user sets it unconditionally). GameVersion is still
    # passed via -p: because Directory.Build.props is conditional.
    $userXml = @"
<?xml version="1.0" encoding="utf-8"?>
<Project><PropertyGroup><BeatSaberDir>$bsDir/</BeatSaberDir></PropertyGroup></Project>
"@
    Set-Content -Path $csprojUser -Value $userXml -Encoding UTF8

    $args = @(
        "publish", $proj,
        "-c", $Configuration,
        "-r", "win-x64",
        "-p:GameVersion=$ver"
    )
    Write-Host "dotnet $($args -join ' ')" -ForegroundColor DarkGray
    Write-Host "  (BeatSaberDir via VainSabers.csproj.user = $bsDir/)" -ForegroundColor DarkGray
    & dotnet @args
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed for $ver (exit $LASTEXITCODE)"
        continue
    }

    # BSMT writes zips to VainSabers/bin/... or publish output; artifactpath logic:
    # Find the newest zip for this version
    $zipPattern = Join-Path $repoRoot "VainSabers/bin/**/*$ver*.zip"
    $zips = Get-ChildItem -Recurse -Filter "*.zip" -Path (Join-Path $repoRoot "VainSabers/bin") -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match [regex]::Escape($ver) -or $_.DirectoryName -match "Release|Debug" } |
        Sort-Object LastWriteTime -Descending

    # Also check BSMT artifactpath output alternative: VainSabers publish folder
    if (-not $zips) {
        $zips = Get-ChildItem -Path (Join-Path $repoRoot "VainSabers") -Filter "*.zip" -Recurse -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending | Select-Object -First 2
    }

    if ($zips) {
        foreach ($z in $zips) {
            $dest = Join-Path $OutputRoot "$($z.BaseName)-bs$ver$($z.Extension)"
            Copy-Item $z.FullName $dest -Force
            Write-Host "  -> Copied $($z.Name) to $dest" -ForegroundColor Green
        }
    } else {
        Write-Host "  Build succeeded but no zip found - check VainSabers/bin output for $ver" -ForegroundColor Yellow
    }
}
} finally {
    # Restore original csproj.user
    if ($hadBackup) {
        Set-Content -Path $csprojUser -Value $backupUser -Encoding UTF8
        Write-Host "`nRestored original VainSabers.csproj.user" -ForegroundColor DarkGray
    } elseif (Test-Path $csprojUser) {
        # we created it, leave it? remove temp
        # keep it as last version's dir, or remove:
        # Remove-Item $csprojUser -Force
    }
}

Write-Host "`nDone. Versioned outputs (if any) in $OutputRoot" -ForegroundColor Cyan
Get-ChildItem $OutputRoot -ErrorAction SilentlyContinue | Format-Table Name, Length, LastWriteTime

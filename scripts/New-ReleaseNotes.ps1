[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageName,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$ReleaseTag,

    [Parameter(Mandatory = $true)]
    [string]$Md5,

    [Parameter(Mandatory = $true)]
    [string]$Sha256,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$releaseNotes = @"
# VoiceHub LanDesktop Plugin $ReleaseTag

## Release Assets

- **Package**: ``$PackageName``
- **Version**: $Version
- **SHA256**: ``$Sha256``
- **MD5**: ``$Md5``

## Installation

Download the ``.laapp`` package and install it through the LanMountainDesktop plugin manager.

## Changes

See commit history for detailed changes in this release.
"@

$directory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($directory)) {
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
}

[System.IO.File]::WriteAllText($OutputPath, $releaseNotes)
Write-Host "Generated release notes at '$OutputPath'."

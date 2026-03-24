[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$PackagePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
    $RepositoryRoot = (Resolve-Path (Join-Path $scriptRoot "..")).Path
}

function Get-VersionCore([string]$Value) {
    $candidate = $Value.Trim()
    if ($candidate.StartsWith("v", [System.StringComparison]::OrdinalIgnoreCase)) {
        $candidate = $candidate.Substring(1)
    }

    $core = ($candidate -split '[-+ ]', 2)[0]
    $parsed = $null
    if (-not [Version]::TryParse($core, [ref]$parsed)) {
        throw "Invalid version '$Value'."
    }

    return $candidate
}

$csprojPath = Join-Path $RepositoryRoot "VoiceHubLanDesktop.csproj"
$manifestPath = Join-Path $RepositoryRoot "plugin.json"

$csprojContent = [System.IO.File]::ReadAllText($csprojPath)
$csprojMatch = [System.Text.RegularExpressions.Regex]::Match(
    $csprojContent,
    "<Version>(?<version>.*?)</Version>",
    [System.Text.RegularExpressions.RegexOptions]::Singleline)
if (-not $csprojMatch.Success) {
    throw "Missing <Version> in '$csprojPath'."
}

$csprojVersion = Get-VersionCore $csprojMatch.Groups["version"].Value
$manifest = Get-Content $manifestPath -Encoding UTF8 -Raw | ConvertFrom-Json
$manifestVersion = Get-VersionCore $manifest.version
$manifestApiVersion = Get-VersionCore $manifest.apiVersion

if ($csprojVersion -ne $manifestVersion) {
    throw "Version mismatch. csproj=$csprojVersion plugin.json=$manifestVersion"
}

if ($manifestApiVersion -ne "4.0.0") {
    throw "API version mismatch. Expected plugin.json apiVersion=4.0.0, actual=$manifestApiVersion"
}

$expectedAssetName = "$($manifest.id).$csprojVersion.laapp"

if ($PackagePath) {
    $resolvedPackagePath = Resolve-Path $PackagePath -ErrorAction Stop
    if ([System.IO.Path]::GetFileName($resolvedPackagePath) -ne $expectedAssetName) {
        throw "Package name mismatch. Expected '$expectedAssetName', actual '$([System.IO.Path]::GetFileName($resolvedPackagePath))'."
    }
}

Write-Host "Plugin version: $csprojVersion"
Write-Host "Plugin API version: $manifestApiVersion"
Write-Host "Expected asset: $expectedAssetName"

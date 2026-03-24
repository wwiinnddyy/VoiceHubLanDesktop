[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$RepositoryRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
    $RepositoryRoot = (Resolve-Path (Join-Path $scriptRoot "..")).Path
}

$normalizedVersion = $Version.Trim()
if ($normalizedVersion.StartsWith("v", [System.StringComparison]::OrdinalIgnoreCase)) {
    $normalizedVersion = $normalizedVersion.Substring(1)
}

$csprojPath = Join-Path $RepositoryRoot "VoiceHubLanDesktop.csproj"
$manifestPath = Join-Path $RepositoryRoot "plugin.json"
$readmePath = Join-Path $RepositoryRoot "README.md"

$csprojContent = [System.IO.File]::ReadAllText($csprojPath)
$updatedCsproj = [System.Text.RegularExpressions.Regex]::Replace(
    $csprojContent,
    "<Version>.*?</Version>",
    "<Version>$normalizedVersion</Version>",
    [System.Text.RegularExpressions.RegexOptions]::Singleline)
[System.IO.File]::WriteAllText($csprojPath, $updatedCsproj)
Write-Host "Updated csproj version to $normalizedVersion"

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$manifest.version = $normalizedVersion
$manifestJson = $manifest | ConvertTo-Json -Depth 10
[System.IO.File]::WriteAllText($manifestPath, $manifestJson)
Write-Host "Updated plugin.json version to $normalizedVersion"

if (Test-Path $readmePath) {
    $readmeContent = [System.IO.File]::ReadAllText($readmePath)
    $updatedReadme = [System.Text.RegularExpressions.Regex]::Replace(
        $readmeContent,
        "Version:\s*`[^`]+`",
        "Version: ``$normalizedVersion``")
    [System.IO.File]::WriteAllText($readmePath, $updatedReadme)
    Write-Host "Updated README.md version to $normalizedVersion"
}

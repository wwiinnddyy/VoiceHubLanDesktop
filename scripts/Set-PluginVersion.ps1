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

$manifest = Get-Content $manifestPath -Encoding UTF8 -Raw | ConvertFrom-Json
$manifest.version = $normalizedVersion
$manifestJson = $manifest | ConvertTo-Json -Depth 10
[System.IO.File]::WriteAllText($manifestPath, $manifestJson)
Write-Host "Updated plugin.json version to $normalizedVersion"

if (Test-Path $readmePath) {
    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    $readme = [System.IO.File]::ReadAllText($readmePath, [System.Text.Encoding]::UTF8)
    $releaseInfoPattern = '(?s)<!-- voicehub-release-info:start -->.*?<!-- voicehub-release-info:end -->'
    if (-not [System.Text.RegularExpressions.Regex]::IsMatch($readme, $releaseInfoPattern)) {
        throw "README.md does not contain the voicehub release info marker block."
    }

    $releaseInfoBlock = @(
        '<!-- voicehub-release-info:start -->'
        "- Current version: $normalizedVersion"
        "- Current release tag: v$normalizedVersion"
        "- Current root package: VoiceHubLanDesktop.$normalizedVersion.laapp"
        '- Published assets: .laapp, market-manifest.json, sha256.txt, md5.txt'
        '<!-- voicehub-release-info:end -->'
    ) -join [Environment]::NewLine

    $updatedReadme = [System.Text.RegularExpressions.Regex]::Replace(
        $readme,
        $releaseInfoPattern,
        $releaseInfoBlock,
        [System.Text.RegularExpressions.RegexOptions]::Singleline)

    [System.IO.File]::WriteAllText($readmePath, $updatedReadme, $utf8NoBom)
    Write-Host "Updated README.md version to $normalizedVersion"
}

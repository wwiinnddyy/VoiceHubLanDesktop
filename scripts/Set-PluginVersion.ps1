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
    $readmeLines = [System.IO.File]::ReadAllLines($readmePath, [System.Text.Encoding]::UTF8)
    $updatedReadmeLines = foreach ($line in $readmeLines) {
        if ($line -match '^(?<prefix>\s*-\s*(?:当前版本|Current version)：?\s*)`[^`]+`(?<suffix>.*)$') {
            $Matches.prefix + '`' + $normalizedVersion + '`' + $Matches.suffix
            continue
        }

        if ($line -match '^(?<prefix>\s*-\s*(?:当前 Release 标签|Current release tag)：?\s*)`[^`]+`(?<suffix>.*)$') {
            $Matches.prefix + '`' + "v$normalizedVersion" + '`' + $Matches.suffix
            continue
        }

        if ($line -match '^(?<prefix>\s*-\s*(?:当前根目录包名|Current root package)：?\s*)`[^`]+`(?<suffix>.*)$') {
            $Matches.prefix + '`' + "VoiceHubLanDesktop.$normalizedVersion.laapp" + '`' + $Matches.suffix
            continue
        }

        $line
    }

    [System.IO.File]::WriteAllLines($readmePath, $updatedReadmeLines, $utf8NoBom)
    Write-Host "Updated README.md version to $normalizedVersion"
}

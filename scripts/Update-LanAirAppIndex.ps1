[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$IndexPath,

    [Parameter(Mandatory)]
    [string]$SyncEntryPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Utf8File([string]$Path, [string]$Content) {
    $encoding = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

$index = Get-Content $IndexPath -Encoding UTF8 -Raw | ConvertFrom-Json
$syncEntry = Get-Content $SyncEntryPath -Encoding UTF8 -Raw | ConvertFrom-Json

if ($null -eq $index.plugins) {
    throw "Market index '$IndexPath' does not contain a plugins array."
}

$plugins = @($index.plugins)
$existingEntry = $plugins | Where-Object { $_.id -eq $syncEntry.id } | Select-Object -First 1
if ($null -ne $existingEntry -and -not [string]::IsNullOrWhiteSpace([string]$existingEntry.publishedAt)) {
    $syncEntry.publishedAt = $existingEntry.publishedAt
}

$updatedPlugins = @($plugins | Where-Object { $_.id -ne $syncEntry.id })
$updatedPlugins += $syncEntry
$index.plugins = @(
    $updatedPlugins |
        Sort-Object `
            @{ Expression = { [string]$_.name }; Ascending = $true },
            @{ Expression = { [string]$_.id }; Ascending = $true }
)
$index.generatedAt = [DateTimeOffset]::UtcNow.ToString("o")

$json = $index | ConvertTo-Json -Depth 20
Write-Utf8File -Path $IndexPath -Content ($json + [Environment]::NewLine)
Write-Host "Updated AirApp Market index at '$IndexPath'."

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$TemplatePath,

    [Parameter(Mandatory = $true)]
    [string]$PackagePath,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Write-Utf8File([string]$Path, [string]$Content) {
    $encoding = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

function Get-PropertyValue($Object, [string]$Name) {
    if ($null -eq $Object) {
        return $null
    }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }
    return $property.Value
}

function Get-ArrayValue($Object, [string]$Name) {
    $value = Get-PropertyValue -Object $Object -Name $Name
    if ($null -eq $value) {
        return @()
    }
    if ($value -is [array]) {
        return $value
    }
    return @($value)
}

$template = Get-Content $TemplatePath -Encoding UTF8 -Raw | ConvertFrom-Json
$resolvedPackagePath = Resolve-Path $PackagePath -ErrorAction Stop
$assetName = [System.IO.Path]::GetFileName($resolvedPackagePath)
$hash = (Get-FileHash -Path $resolvedPackagePath -Algorithm SHA256).Hash.ToLowerInvariant()
$packageSize = (Get-Item $resolvedPackagePath).Length

$archive = [System.IO.Compression.ZipFile]::OpenRead($resolvedPackagePath)
try {
    $manifestEntry = $archive.Entries | Where-Object { $_.FullName -eq "plugin.json" } | Select-Object -First 1
    if ($null -eq $manifestEntry) {
        throw "Plugin package '$resolvedPackagePath' does not contain 'plugin.json'."
    }

    $stream = $null
    $reader = $null
    try {
        $stream = $manifestEntry.Open()
        $reader = [System.IO.StreamReader]::new($stream)
        $manifestJson = $reader.ReadToEnd()
    }
    finally {
        if ($reader) {
            $reader.Dispose()
        }

        if ($stream) {
            $stream.Dispose()
        }
    }
}
finally {
    $archive.Dispose()
}

$manifest = $manifestJson | ConvertFrom-Json
$manifestVersion = [string](Get-PropertyValue $manifest "version")
if ([string]::IsNullOrWhiteSpace($manifestVersion)) {
    throw "Plugin manifest inside '$resolvedPackagePath' is missing 'version'."
}

if ($manifestVersion -ne $Version) {
    throw "Requested version '$Version' does not match package manifest version '$manifestVersion'."
}

$sharedContracts = @(
    Get-ArrayValue -Object $manifest -Name "sharedContracts" |
        ForEach-Object {
            [pscustomobject][ordered]@{
                id = [string](Get-PropertyValue $_ "id")
                version = [string](Get-PropertyValue $_ "version")
                assemblyName = [string](Get-PropertyValue $_ "assemblyName")
            }
        }
)

$tags = @(
    Get-ArrayValue -Object $template -Name "tags" |
        Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) } |
        ForEach-Object { [string]$_ }
)

$timestamp = [DateTimeOffset]::UtcNow.ToString("o")

$entry = [pscustomobject][ordered]@{
    id = [string](Get-PropertyValue $manifest "id")
    name = [string](Get-PropertyValue $manifest "name")
    description = [string](Get-PropertyValue $manifest "description")
    author = [string](Get-PropertyValue $manifest "author")
    version = $manifestVersion
    apiVersion = [string](Get-PropertyValue $manifest "apiVersion")
    sharedContracts = $sharedContracts
    minHostVersion = [string](Get-PropertyValue $template "minHostVersion")
    downloadUrl = ""
    sha256 = $hash
    packageSizeBytes = $packageSize
    iconUrl = [string](Get-PropertyValue $template "iconUrl")
    releaseTag = "v$manifestVersion"
    releaseAssetName = $assetName
    projectUrl = [string](Get-PropertyValue $template "projectUrl")
    readmeUrl = [string](Get-PropertyValue $template "readmeUrl")
    homepageUrl = [string](Get-PropertyValue $template "homepageUrl")
    repositoryUrl = [string](Get-PropertyValue $template "repositoryUrl")
    tags = $tags
    publishedAt = $timestamp
    updatedAt = $timestamp
    releaseNotes = [string](Get-PropertyValue $template "releaseNotes")
}

$directory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($directory)) {
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
}

$json = $entry | ConvertTo-Json -Depth 20
Write-Utf8File -Path $OutputPath -Content ($json + [Environment]::NewLine)
Write-Host "Generated market sync metadata at '$OutputPath'."

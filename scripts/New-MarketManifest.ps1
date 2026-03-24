[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$TemplatePath,

    [Parameter(Mandatory = $true)]
    [string]$PackagePath,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$ReleaseTag,

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

function Get-RepositoryInfo([string]$RepositoryUrl) {
    $uri = [Uri]$RepositoryUrl
    if ($uri.Host -ne "github.com") {
        throw "Unsupported repository host in '$RepositoryUrl'."
    }

    $segments = $uri.AbsolutePath.Trim("/") -split "/"
    if ($segments.Length -ne 2) {
        throw "Repository URL '$RepositoryUrl' must point to the GitHub repository root."
    }

    return @{
        Owner = $segments[0]
        Name = $segments[1]
    }
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

function Get-PackageManifest([string]$ArchivePath) {
    $archive = [System.IO.Compression.ZipFile]::OpenRead($ArchivePath)
    try {
        $manifestEntry = $archive.Entries | Where-Object { $_.FullName -eq "plugin.json" } | Select-Object -First 1
        if ($null -eq $manifestEntry) {
            throw "Plugin package '$ArchivePath' does not contain 'plugin.json'."
        }

        $stream = $null
        $reader = $null
        try {
            $stream = $manifestEntry.Open()
            $reader = [System.IO.StreamReader]::new($stream, [System.Text.UTF8Encoding]::UTF8, $true)
            return $reader.ReadToEnd() | ConvertFrom-Json
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
}

$template = Get-Content $TemplatePath -Encoding UTF8 -Raw | ConvertFrom-Json
$resolvedPackagePath = Resolve-Path $PackagePath -ErrorAction Stop
$assetName = [System.IO.Path]::GetFileName($resolvedPackagePath)
$sha256 = (Get-FileHash -Path $resolvedPackagePath -Algorithm SHA256).Hash.ToLowerInvariant()
$md5 = (Get-FileHash -Path $resolvedPackagePath -Algorithm MD5).Hash.ToLowerInvariant()
$packageSizeBytes = (Get-Item $resolvedPackagePath).Length
$generatedAt = [DateTimeOffset]::UtcNow.ToString("o")

$manifest = Get-PackageManifest -ArchivePath $resolvedPackagePath
$manifestId = [string](Get-PropertyValue $manifest "id")
$manifestName = [string](Get-PropertyValue $manifest "name")
$manifestDescription = [string](Get-PropertyValue $manifest "description")
$manifestAuthor = [string](Get-PropertyValue $manifest "author")
$manifestVersion = [string](Get-PropertyValue $manifest "version")
$manifestApiVersion = [string](Get-PropertyValue $manifest "apiVersion")
$entranceAssembly = [string](Get-PropertyValue $manifest "entranceAssembly")

if ([string]::IsNullOrWhiteSpace($manifestId)) {
    throw "Plugin manifest inside '$resolvedPackagePath' is missing 'id'."
}

if ([string]::IsNullOrWhiteSpace($manifestName)) {
    throw "Plugin manifest inside '$resolvedPackagePath' is missing 'name'."
}

if ([string]::IsNullOrWhiteSpace($manifestAuthor)) {
    throw "Plugin manifest inside '$resolvedPackagePath' is missing 'author'."
}

if ([string]::IsNullOrWhiteSpace($manifestVersion)) {
    throw "Plugin manifest inside '$resolvedPackagePath' is missing 'version'."
}

if ($manifestVersion -ne $Version) {
    throw "Requested version '$Version' does not match package manifest version '$manifestVersion'."
}

if ([string]::IsNullOrWhiteSpace($entranceAssembly)) {
    $entranceAssembly = "$manifestId.dll"
}

$repositoryUrl = [string](Get-PropertyValue $template "repositoryUrl")
if ([string]::IsNullOrWhiteSpace($repositoryUrl)) {
    $repositoryUrl = [string](Get-PropertyValue $template "projectUrl")
}

if ([string]::IsNullOrWhiteSpace($repositoryUrl)) {
    throw "Template is missing repositoryUrl/projectUrl."
}

$repo = Get-RepositoryInfo -RepositoryUrl $repositoryUrl
$releaseAssetUrl = "https://github.com/$($repo.Owner)/$($repo.Name)/releases/download/$ReleaseTag/$assetName"
$rawFallbackUrl = "https://raw.githubusercontent.com/$($repo.Owner)/$($repo.Name)/main/$assetName"
$workspaceLocalUrl = "./$assetName"
$minHostVersion = [string](Get-PropertyValue $template "minHostVersion")
$iconUrl = [string](Get-PropertyValue $template "iconUrl")
$projectUrl = [string](Get-PropertyValue $template "projectUrl")
$readmeUrl = [string](Get-PropertyValue $template "readmeUrl")
$homepageUrl = [string](Get-PropertyValue $template "homepageUrl")
$repositoryUrl = [string](Get-PropertyValue $template "repositoryUrl")
$releaseNotes = [string](Get-PropertyValue $template "releaseNotes")
$tags = @(
    Get-ArrayValue -Object $template -Name "tags" |
        Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) } |
        ForEach-Object { [string]$_ }
)

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

$packageSources = @(
    [pscustomobject][ordered]@{
        kind = "releaseAsset"
        url = $releaseAssetUrl
        sha256 = $sha256
        packageSizeBytes = $packageSizeBytes
    },
    [pscustomobject][ordered]@{
        kind = "rawFallback"
        url = $rawFallbackUrl
        sha256 = $sha256
        packageSizeBytes = $packageSizeBytes
    },
    [pscustomobject][ordered]@{
        kind = "workspaceLocal"
        url = $workspaceLocalUrl
        sha256 = $sha256
        packageSizeBytes = $packageSizeBytes
    }
)

$manifestObject = [pscustomobject][ordered]@{
    schemaVersion = "2.0.0"
    generatedAt = $generatedAt
    manifest = [pscustomobject][ordered]@{
        id = $manifestId
        name = $manifestName
        description = $manifestDescription
        author = $manifestAuthor
        version = $manifestVersion
        apiVersion = $manifestApiVersion
        entranceAssembly = $entranceAssembly
        sharedContracts = $sharedContracts
    }
    compatibility = [pscustomobject][ordered]@{
        minHostVersion = $minHostVersion
    }
    repository = [pscustomobject][ordered]@{
        iconUrl = $iconUrl
        projectUrl = $projectUrl
        readmeUrl = $readmeUrl
        homepageUrl = $homepageUrl
        repositoryUrl = $repositoryUrl
        tags = $tags
        releaseNotes = $releaseNotes
    }
    publication = [pscustomobject][ordered]@{
        releaseTag = $ReleaseTag
        releaseAssetName = $assetName
        publishedAt = $generatedAt
        updatedAt = $generatedAt
        packageSizeBytes = $packageSizeBytes
        sha256 = $sha256
        md5 = $md5
        packageSources = $packageSources
    }
}

$directory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($directory)) {
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
}

$json = $manifestObject | ConvertTo-Json -Depth 20
Write-Utf8File -Path $OutputPath -Content ($json + [Environment]::NewLine)
Write-Host "Generated market manifest at '$OutputPath'."

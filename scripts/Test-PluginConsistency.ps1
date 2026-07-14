[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$PackagePath,
    [string]$MarketManifestPath
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

function Get-PackageEntryText([string]$ArchivePath, [string]$EntryName) {
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $archive = [System.IO.Compression.ZipFile]::OpenRead($ArchivePath)
    try {
        $entry = $archive.Entries | Where-Object { $_.FullName -eq $EntryName } | Select-Object -First 1
        if ($null -eq $entry) {
            throw "Package '$ArchivePath' is missing '$EntryName'."
        }

        $stream = $entry.Open()
        $reader = [System.IO.StreamReader]::new($stream, [System.Text.UTF8Encoding]::UTF8, $true)
        try {
            return $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
            $stream.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }
}

$csprojPath = Join-Path $RepositoryRoot "VoiceHubLanDesktop.csproj"
$manifestPath = Join-Path $RepositoryRoot "plugin.json"
$marketTemplatePath = Join-Path $RepositoryRoot "airappmarket-entry.template.json"
$nugetConfigPath = Join-Path $RepositoryRoot "NuGet.config"

if (-not (Test-Path $csprojPath)) { throw "Project file not found: $csprojPath" }
if (-not (Test-Path $manifestPath)) { throw "Manifest file not found: $manifestPath" }
if (-not (Test-Path $marketTemplatePath)) { throw "Market template not found: $marketTemplatePath" }
if (-not (Test-Path $nugetConfigPath)) { throw "NuGet config not found: $nugetConfigPath" }

$csprojContent = [System.IO.File]::ReadAllText($csprojPath)
$csprojMatch = [System.Text.RegularExpressions.Regex]::Match(
    $csprojContent,
    "<Version>(?<version>.*?)</Version>",
    [System.Text.RegularExpressions.RegexOptions]::Singleline)
if (-not $csprojMatch.Success) {
    throw "Missing <Version> in '$csprojPath'."
}

if ($csprojContent -notmatch '<PackageReference\s+Include="LanMountainDesktop\.PluginSdk"\s+Version="5\.0\.0"') {
    throw "VoiceHub must reference LanMountainDesktop.PluginSdk 5.0.0."
}

if ($csprojContent -match 'LanMountainDesktop\.AirAppSdk') {
    throw "Legacy LanMountainDesktop.AirAppSdk references are not supported."
}

if ($csprojContent -match '<PackageReference\s+Include="(?:Avalonia|Avalonia\.Desktop|Avalonia\.Themes\.Fluent|FluentAvaloniaUI|FluentAvalonia\.FluentUI)"') {
    throw "UI dependencies must flow through PluginSdk 5.0.0 so they stay aligned with the host."
}

if ($csprojContent -notmatch '<RestorePackagesPath>\$\(MSBuildProjectDirectory\)\\\.nuget\\packages</RestorePackagesPath>') {
    throw "RestorePackagesPath must isolate packages under the repository .nuget/packages directory."
}

$nugetConfig = [xml](Get-Content $nugetConfigPath -Encoding UTF8 -Raw)
$globalPackagesFolder = $nugetConfig.configuration.config.add |
    Where-Object { $_.key -eq "globalPackagesFolder" } |
    Select-Object -First 1
if ($null -eq $globalPackagesFolder -or [string]$globalPackagesFolder.value -ne ".nuget/packages") {
    throw "NuGet.config must set globalPackagesFolder to .nuget/packages."
}

$csprojVersion = Get-VersionCore $csprojMatch.Groups["version"].Value
$manifest = Get-Content $manifestPath -Encoding UTF8 -Raw | ConvertFrom-Json
$manifestVersion = Get-VersionCore $manifest.version
$manifestApiVersion = Get-VersionCore $manifest.apiVersion

if ($csprojVersion -ne $manifestVersion) {
    throw "Version mismatch. csproj=$csprojVersion plugin.json=$manifestVersion"
}

if ($manifestApiVersion -ne "5.0.0") {
    throw "API version mismatch. Expected plugin.json apiVersion=5.0.0, actual=$manifestApiVersion"
}

if ($manifest.id -ne "VoiceHubLanDesktop") {
    throw "Plugin id mismatch. Expected VoiceHubLanDesktop, actual=$($manifest.id)"
}

if ($manifest.entranceAssembly -ne "VoiceHubLanDesktop.dll") {
    throw "Entrance assembly mismatch. Expected VoiceHubLanDesktop.dll, actual=$($manifest.entranceAssembly)"
}

if ($manifest.runtime.mode -ne "in-proc") {
    throw "Runtime mode mismatch. Expected in-proc, actual=$($manifest.runtime.mode)"
}

$marketTemplate = Get-Content $marketTemplatePath -Encoding UTF8 -Raw | ConvertFrom-Json
if ([string]$marketTemplate.minHostVersion -ne "0.8.6") {
    throw "PluginSdk v5 market entries require minHostVersion=0.8.6, actual=$($marketTemplate.minHostVersion)"
}

$expectedAssetName = "$($manifest.id).$csprojVersion.laapp"

if ($PackagePath) {
    $resolvedPackagePath = Resolve-Path $PackagePath -ErrorAction Stop
    if ([System.IO.Path]::GetFileName($resolvedPackagePath) -ne $expectedAssetName) {
        throw "Package name mismatch. Expected '$expectedAssetName', actual '$([System.IO.Path]::GetFileName($resolvedPackagePath))'."
    }

    $packageManifest = Get-PackageEntryText -ArchivePath $resolvedPackagePath -EntryName "plugin.json" | ConvertFrom-Json
    if ($packageManifest.id -ne $manifest.id -or
        $packageManifest.version -ne $manifest.version -or
        $packageManifest.apiVersion -ne $manifest.apiVersion -or
        $packageManifest.entranceAssembly -ne $manifest.entranceAssembly -or
        $packageManifest.runtime.mode -ne $manifest.runtime.mode) {
        throw "Package manifest does not match repository plugin.json."
    }

    [void](Get-PackageEntryText -ArchivePath $resolvedPackagePath -EntryName "VoiceHubLanDesktop.dll")
    [void](Get-PackageEntryText -ArchivePath $resolvedPackagePath -EntryName "VoiceHubLanDesktop.deps.json")

    $assetsPath = Join-Path $RepositoryRoot "obj\project.assets.json"
    if (-not (Test-Path $assetsPath)) {
        throw "Resolved dependency assets were not found at '$assetsPath'."
    }
    $assets = Get-Content $assetsPath -Encoding UTF8 -Raw | ConvertFrom-Json
    $libraryNames = @($assets.libraries.PSObject.Properties.Name)
    if ($libraryNames -notcontains "Avalonia/12.1.0" -or $libraryNames -notcontains "FluentAvaloniaUI/3.0.1") {
        throw "Resolved UI dependencies must be Avalonia 12.1.0 and FluentAvaloniaUI 3.0.1."
    }
    $expectedPackagesRoot = [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot ".nuget\packages")).TrimEnd('\', '/')
    $resolvedPackageFolders = @($assets.packageFolders.PSObject.Properties.Name | ForEach-Object { ([System.IO.Path]::GetFullPath($_)).TrimEnd('\', '/') })
    if ($resolvedPackageFolders.Count -ne 1 -or $resolvedPackageFolders[0] -ne $expectedPackagesRoot) {
        throw "Restore must use repository-local package cache '$expectedPackagesRoot'; actual='$($resolvedPackageFolders -join ',')'."
    }
}

if ($MarketManifestPath) {
    if (-not $PackagePath) {
        throw "PackagePath is required when MarketManifestPath is provided."
    }

    $resolvedPackagePath = (Resolve-Path $PackagePath -ErrorAction Stop).Path
    $resolvedMarketManifestPath = (Resolve-Path $MarketManifestPath -ErrorAction Stop).Path
    $marketManifest = Get-Content $resolvedMarketManifestPath -Encoding UTF8 -Raw | ConvertFrom-Json
    $packageManifest = Get-PackageEntryText -ArchivePath $resolvedPackagePath -EntryName "plugin.json" | ConvertFrom-Json
    $packageHash = (Get-FileHash $resolvedPackagePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $packageSize = (Get-Item $resolvedPackagePath).Length
    $assetName = [System.IO.Path]::GetFileName($resolvedPackagePath)

    if ($marketManifest.schemaVersion -ne "2.0.0" -or
        $marketManifest.compatibility.minHostVersion -ne "0.8.6" -or
        $marketManifest.compatibility.apiVersion -ne "5.0.0") {
        throw "Market manifest schema or compatibility metadata is invalid."
    }
    if ($marketManifest.manifest.id -ne $packageManifest.id -or
        $marketManifest.manifest.version -ne $packageManifest.version -or
        $marketManifest.manifest.apiVersion -ne $packageManifest.apiVersion -or
        $marketManifest.manifest.entranceAssembly -ne $packageManifest.entranceAssembly) {
        throw "Market manifest plugin metadata does not match package plugin.json."
    }
    if ($marketManifest.publication.releaseAssetName -ne $assetName -or
        $marketManifest.publication.sha256 -ne $packageHash -or
        [long]$marketManifest.publication.packageSizeBytes -ne $packageSize) {
        throw "Market publication metadata does not match the package asset."
    }

    $sources = @($marketManifest.publication.packageSources)
    if ($sources.Count -ne 3) {
        throw "Market manifest must contain exactly three package sources."
    }
    foreach ($source in $sources) {
        if ($source.assetName -ne $assetName -or $source.sha256 -ne $packageHash -or [long]$source.sizeBytes -ne $packageSize) {
            throw "Market package source '$($source.kind)' does not match the package asset."
        }
    }

    Write-Host "Market/package joint validation passed: $assetName"
}

Write-Host "Plugin version: $csprojVersion"
Write-Host "Plugin API version: $manifestApiVersion"
Write-Host "Expected asset: $expectedAssetName"

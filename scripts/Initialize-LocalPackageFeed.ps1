[CmdletBinding()]
param(
    [string]$FeedPath,
    [string]$CoreContractsProjectPath,
    [string]$PluginSdkProjectPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($FeedPath)) {
    $scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
    $FeedPath = Join-Path (Resolve-Path (Join-Path $scriptRoot "..")).Path "packages"
}

if ([string]::IsNullOrWhiteSpace($PluginSdkProjectPath)) {
    $PluginSdkProjectPath = (Resolve-Path "..\LanMountainDesktop\LanMountainDesktop.PluginSdk\LanMountainDesktop.PluginSdk.csproj").Path
}

if ([string]::IsNullOrWhiteSpace($CoreContractsProjectPath)) {
    $CoreContractsProjectPath = (Resolve-Path "..\LanMountainDesktop\LanMountainDesktop.Shared.Contracts\LanMountainDesktop.Shared.Contracts.csproj").Path
}

# Determine additional project paths based on SDK project location
$sdkProjectDir = Split-Path -Parent $PluginSdkProjectPath
$lanMountainRoot = Split-Path -Parent $sdkProjectDir

$pluginIsolationContractsProjectPath = Join-Path $lanMountainRoot "LanMountainDesktop.PluginIsolation.Contracts\LanMountainDesktop.PluginIsolation.Contracts.csproj"
$sharedIPCProjectPath = Join-Path $lanMountainRoot "LanMountainDesktop.Shared.IPC\LanMountainDesktop.Shared.IPC.csproj"

function Pack-Project([string]$ProjectPath, [string]$OutputDirectory) {
    if (-not (Test-Path $ProjectPath)) {
        throw "Project '$ProjectPath' was not found."
    }

    dotnet pack $ProjectPath -c Release -o $OutputDirectory -p:ContinuousIntegrationBuild=true | Out-Host
}

New-Item -ItemType Directory -Force -Path $FeedPath | Out-Null

# Pack projects in dependency order
Pack-Project -ProjectPath $CoreContractsProjectPath -OutputDirectory $FeedPath

if (Test-Path $pluginIsolationContractsProjectPath) {
    Pack-Project -ProjectPath $pluginIsolationContractsProjectPath -OutputDirectory $FeedPath
}

if (Test-Path $sharedIPCProjectPath) {
    Pack-Project -ProjectPath $sharedIPCProjectPath -OutputDirectory $FeedPath
}

Pack-Project -ProjectPath $PluginSdkProjectPath -OutputDirectory $FeedPath

Write-Host "Local package feed initialized at '$FeedPath'."

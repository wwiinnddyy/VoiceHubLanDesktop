[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$LanMountainDesktopPath,
    [switch]$SkipLocalFeed
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) "..")).Path
Push-Location $repoRoot
try {
    if (-not $SkipLocalFeed) {
        if ([string]::IsNullOrWhiteSpace($LanMountainDesktopPath)) {
            $candidate = Resolve-Path "..\LanMountainDesktop" -ErrorAction SilentlyContinue
            if ($candidate) {
                $LanMountainDesktopPath = $candidate.Path
            }
        }

        if ([string]::IsNullOrWhiteSpace($LanMountainDesktopPath)) {
            throw "LanMountainDesktopPath was not provided and ../LanMountainDesktop was not found."
        }

        .\scripts\Initialize-LocalPackageFeed.ps1 `
            -FeedPath (Join-Path $repoRoot "packages") `
            -PluginSdkProjectPath (Join-Path $LanMountainDesktopPath "LanMountainDesktop.PluginSdk\LanMountainDesktop.PluginSdk.csproj") `
            -CoreContractsProjectPath (Join-Path $LanMountainDesktopPath "LanMountainDesktop.Shared.Contracts\LanMountainDesktop.Shared.Contracts.csproj")
    }

    $localPackagesRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot ".nuget\packages"))
    $repositoryPrefix = $repoRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if (-not $localPackagesRoot.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean NuGet cache outside repository root: '$localPackagesRoot'."
    }
    if (Test-Path $localPackagesRoot) {
        Remove-Item -LiteralPath $localPackagesRoot -Recurse -Force
    }

    .\scripts\Test-PluginConsistency.ps1 -RepositoryRoot $repoRoot
    dotnet restore .\VoiceHubLanDesktop.csproj --configfile .\NuGet.config --force --no-cache -v minimal
    dotnet build .\VoiceHubLanDesktop.csproj -c $Configuration --no-restore -v minimal

    $manifest = Get-Content .\plugin.json -Encoding UTF8 -Raw | ConvertFrom-Json
    $packagePath = Join-Path $repoRoot "$($manifest.id).$($manifest.version).laapp"
    if (-not (Test-Path $packagePath)) {
        throw "No .laapp package was produced at '$packagePath'."
    }

    .\scripts\Test-PluginConsistency.ps1 -RepositoryRoot $repoRoot -PackagePath $packagePath
    Write-Host "Built package: $packagePath"
}
finally {
    Pop-Location
}

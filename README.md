# VoiceHubLanDesktop

LanMountainDesktop plugin for showing the VoiceHub campus radio playlist.

## Features

- Show the current VoiceHub public schedule
- Display songs in play order
- Support custom API URL
- Support configurable refresh interval
- Support showing or hiding requesters
- Support configurable max item count
- Support localization
- Auto-scale for small widget sizes

## Install

Download the latest `.laapp` from [Releases](https://github.com/wwiinnddyy/VoiceHubLanDesktop/releases) and install it in LanMountainDesktop. Official releases also include `market-manifest.json` for the market aggregator.

## Release Info

<!-- voicehub-release-info:start -->
- Current version: 0.0.3
- Current release tag: v0.0.3
- Current root package: VoiceHubLanDesktop.0.0.3.laapp
- Published assets: .laapp, market-manifest.json, sha256.txt, md5.txt
<!-- voicehub-release-info:end -->

## Manual Install

1. Download the latest `.laapp`
2. Copy it into the LanMountainDesktop plugin folder

## Build

### Requirements

- .NET 10.0 SDK
- LanMountainDesktop.PluginSdk 4.0.0

### Local build

```powershell
# Initialize the local package feed
.\scripts\Initialize-LocalPackageFeed.ps1

# Build the project
dotnet build -c Release
```

### Validation

```powershell
# Validate plugin consistency
.\scripts\Test-PluginConsistency.ps1
```

## Configuration

Find the "VoiceHub settings" page in LanMountainDesktop:

| Setting | Description | Default |
|---------|-------------|---------|
| API URL | VoiceHub public API endpoint | https://voicehub.lao-shui.top/api/songs/public |
| Refresh interval | Auto refresh interval in minutes | 60 |
| Show requester | Whether to show requesters | Yes |
| Max items | Maximum number of songs to show | 10 |

## Project Layout

```text
VoiceHubLanDesktop/
├── .github/
│   └── workflows/
│       ├── voicehub-plugin-ci.yml
│       └── voicehub-plugin-release.yml
├── Localization/
│   ├── zh-CN.json
│   └── en-US.json
├── Models/
│   └── VoiceHubModels.cs
├── Services/
│   └── VoiceHubSettingsService.cs
├── Widgets/
│   └── VoiceHubPlaylistWidget.cs
├── scripts/
│   ├── Initialize-LocalPackageFeed.ps1
│   ├── New-MarketManifest.ps1
│   ├── New-ReleaseNotes.ps1
│   ├── Set-PluginVersion.ps1
│   └── Test-PluginConsistency.ps1
├── airappmarket-entry.template.json
├── NuGet.config
├── Plugin.cs
├── plugin.json
└── VoiceHubLanDesktop.csproj
```

## License

MIT License

## Author

LaoShui

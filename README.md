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
- Current version: 1.0.0
- Current release tag: v1.0.0
- Current root package: VoiceHubLanDesktop.1.0.0.laapp
- Published assets: .laapp, market-manifest.json, sha256.txt, md5.txt
<!-- voicehub-release-info:end -->

## Manual Install

1. Download the latest `.laapp`
2. Copy it into the LanMountainDesktop plugin folder

## Build

### Requirements

- .NET 10.0 SDK
- LanMountainDesktop.AirAppSdk 1.0.0, published on GitHub Packages. Add the source once with a PAT that has `read:packages`:

  ```powershell
  dotnet nuget add source https://nuget.pkg.github.com/wwiinnddyy/index.json --name lanmountain --username <your-github-username> --password <PAT> --store-password-in-clear-text
  ```

### Local build

```powershell
dotnet build VoiceHubLanDesktop.csproj -c Release
```

The AirApp SDK's MSBuild target packages the build output into `VoiceHubLanDesktop.<version>.laapp` automatically.

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
│       ├── ci.yml
│       └── release.yml
├── Localization/
│   ├── zh-CN.json
│   └── en-US.json
├── Models/
│   └── VoiceHubModels.cs
├── Services/
│   └── VoiceHubSettingsService.cs
├── Widgets/
│   └── VoiceHubPlaylistWidget.cs
├── airappmarket-entry.template.json
├── NuGet.config
├── Plugin.cs
├── airapp.json
└── VoiceHubLanDesktop.csproj
```

## License

MIT License

## Author

LaoShui

# VoiceHubLanDesktop

声动校园歌单 - LanMountainDesktop 桌面插件

## 功能特性

- 展示VoiceHub广播站当日排期歌曲
- 按播放顺序显示歌曲信息（序号、歌手-歌名、点歌人、投票数）
- 支持自定义API地址
- 支持自定义刷新间隔（1-1440分钟）
- 支持显示/隐藏点歌人
- 支持自定义最大显示数量（1-50首）
- 支持中英文本地化
- 等比例缩放（最小3×4格子）

## 安装方式

### 从Release安装

从 [Releases](https://github.com/laoshuikaixue/VoiceHubLanDesktop/releases) 页面下载最新的 `.laapp` 文件，然后在 LanMountainDesktop 中安装插件。

### 手动安装

1. 下载最新的 `.laapp` 文件
2. 将文件复制到 LanMountainDesktop 的插件目录

## 开发构建

### 环境要求

- .NET 10.0 SDK
- LanMountainDesktop.PluginSdk 4.0.0

### 本地开发

```powershell
# 初始化本地包源
.\scripts\Initialize-LocalPackageFeed.ps1

# 构建项目
dotnet build -c Release
```

### 运行测试

```powershell
# 验证插件一致性
.\scripts\Test-PluginConsistency.ps1
```

## 配置说明

在 LanMountainDesktop 设置中找到"声动校园设置"页面：

| 设置项 | 说明 | 默认值 |
|--------|------|--------|
| API地址 | VoiceHub广播站API地址 | https://voicehub.lao-shui.top/api/songs/public |
| 刷新间隔 | 自动刷新间隔（分钟） | 60 |
| 显示点歌人 | 是否显示点歌人名称 | 是 |
| 最大显示数量 | 最多显示歌曲数量 | 10 |

## 项目结构

```
VoiceHubLanDesktop/
├── .github/
│   └── workflows/
│       ├── voicehub-plugin-ci.yml        # CI工作流
│       └── voicehub-plugin-release.yml   # 发布工作流
├── Localization/
│   ├── zh-CN.json                       # 中文本地化
│   └── en-US.json                       # 英文本地化
├── Models/
│   └── VoiceHubModels.cs                # 数据模型
├── Services/
│   └── VoiceHubSettingsService.cs       # 设置服务
├── Widgets/
│   └── VoiceHubPlaylistWidget.cs        # 桌面组件
├── scripts/
│   ├── Initialize-LocalPackageFeed.ps1  # 初始化本地包源
│   ├── New-AirAppMarketSync.ps1         # 生成市场同步数据
│   ├── New-ReleaseNotes.ps1             # 生成发布说明
│   ├── Set-PluginVersion.ps1            # 设置插件版本
│   └── Test-PluginConsistency.ps1       # 测试插件一致性
├── airappmarket-entry.template.json     # 市场条目模板
├── NuGet.config                         # NuGet配置
├── Plugin.cs                            # 插件入口
├── plugin.json                          # 插件清单
└── VoiceHubLanDesktop.csproj            # 项目文件
```

## 许可证

MIT License

## 作者

LaoShui

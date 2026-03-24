using System.IO;
using LanMountainDesktop.PluginSdk;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VoiceHubLanDesktop.Services;
using VoiceHubLanDesktop.Widgets;

namespace VoiceHubLanDesktop;

[PluginEntrance]
public sealed class Plugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(services);

        var localizer = CreateLocalizer(context);

        services.AddSingleton(provider =>
        {
            var runtimeContext = provider.GetRequiredService<IPluginRuntimeContext>();
            Directory.CreateDirectory(runtimeContext.DataDirectory);
            return new VoiceHubSettingsService(runtimeContext.DataDirectory);
        });

        services.AddSingleton<VoiceHubDataService>();

        services.AddPluginSettingsSection(
            id: "voicehub-settings",
            titleLocalizationKey: "settings.page_title",
            configure: builder =>
            {
                builder.AddText(
                    key: "apiUrl",
                    titleLocalizationKey: "settings.api_url",
                    descriptionLocalizationKey: "settings.api_url_desc",
                    defaultValue: "https://voicehub.lao-shui.top/api/songs/public");

                builder.AddNumber(
                    key: "refreshIntervalMinutes",
                    titleLocalizationKey: "settings.refresh_interval",
                    descriptionLocalizationKey: "settings.refresh_interval_desc",
                    defaultValue: 60,
                    minimum: 1,
                    maximum: 1440);

                builder.AddToggle(
                    key: "showRequester",
                    titleLocalizationKey: "settings.show_requester",
                    descriptionLocalizationKey: "settings.show_requester_desc",
                    defaultValue: true);

                builder.AddNumber(
                    key: "maxDisplayCount",
                    titleLocalizationKey: "settings.max_display_count",
                    descriptionLocalizationKey: "settings.max_display_count_desc",
                    defaultValue: 10,
                    minimum: 1,
                    maximum: 50);
            },
            descriptionLocalizationKey: "plugin.description",
            iconKey: "MusicNote",
            sortOrder: 0);

        services.AddPluginDesktopComponent<VoiceHubPlaylistWidget>(
            CreatePlaylistComponentOptions(localizer));
    }

    private static PluginLocalizer CreateLocalizer(HostBuilderContext context)
    {
        var pluginDirectory = context.Properties.TryGetValue("LanMountainDesktop.PluginDirectory", out var directoryValue) &&
                              directoryValue is string resolvedPluginDirectory &&
                              !string.IsNullOrWhiteSpace(resolvedPluginDirectory)
            ? resolvedPluginDirectory
            : AppContext.BaseDirectory;

        var properties = context.Properties
            .Where(pair => pair.Key is string)
            .ToDictionary(pair => (string)pair.Key, pair => (object?)pair.Value, System.StringComparer.OrdinalIgnoreCase);

        return new PluginLocalizer(pluginDirectory, PluginLocalizer.ResolveLanguageCode(properties));
    }

    private static PluginDesktopComponentOptions CreatePlaylistComponentOptions(PluginLocalizer localizer)
    {
        return new PluginDesktopComponentOptions
        {
            ComponentId = "VoiceHubLanDesktop.Playlist",
            DisplayName = localizer.GetString("widget.display_name", "声动校园歌单"),
            DisplayNameLocalizationKey = "widget.display_name",
            IconKey = "MusicNote",
            Category = localizer.GetString("widget.category", "声动校园"),
            MinWidthCells = 3,
            MinHeightCells = 4,
            AllowDesktopPlacement = true,
            AllowStatusBarPlacement = false,
            ResizeMode = PluginDesktopComponentResizeMode.Proportional,
            CornerRadiusPreset = PluginCornerRadiusPreset.Default
        };
    }
}

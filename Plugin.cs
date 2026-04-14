using System.IO;
using LanMountainDesktop.PluginSdk;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VoiceHubLanDesktop.Services;
using VoiceHubLanDesktop.ViewModels;
using VoiceHubLanDesktop.Views;
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

        // Register ViewModel for settings page
        services.AddTransient<VoiceHubSettingsViewModel>(provider =>
        {
            var settingsService = provider.GetRequiredService<VoiceHubSettingsService>();
            return new VoiceHubSettingsViewModel(settingsService, localizer);
        });

        // Register custom settings page with VoiceHubSettingsView (Fluent Avalonia)
        services.AddPluginSettingsSection<VoiceHubSettingsView>(
            id: "voicehub-settings",
            titleLocalizationKey: "settings.page_title",
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

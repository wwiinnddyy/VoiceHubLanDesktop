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

        services.AddSingleton(provider =>
        {
            var runtimeContext = provider.GetRequiredService<IPluginRuntimeContext>();
            Directory.CreateDirectory(runtimeContext.DataDirectory);
            return new VoiceHubSettingsService(runtimeContext.DataDirectory);
        });

        services.AddSingleton<VoiceHubDataService>();
        services.AddTransient<VoiceHubSettingsViewModel>();

        services.AddPluginSettingsSection<VoiceHubSettingsView>(
            id: "connection",
            titleLocalizationKey: "VoiceHub settings",
            descriptionLocalizationKey: "Connection, refresh, and playlist display settings.",
            iconKey: "MusicNote2",
            sortOrder: 0);

        services.AddPluginDesktopComponent<VoiceHubPlaylistWidget>(new PluginDesktopComponentOptions
        {
            ComponentId = "voicehub-playlist",
            DisplayName = "VoiceHub campus playlist",
            Description = "Displays the public VoiceHub broadcast playlist.",
            Category = "Entertainment",
            IconKey = "MusicNote2",
            MinWidthCells = 3,
            MinHeightCells = 4,
            AllowDesktopPlacement = true,
            AllowStatusBarPlacement = false,
            ResizeMode = PluginDesktopComponentResizeMode.Free,
            CornerRadiusPreset = PluginCornerRadiusPreset.Component
        });
    }
}

using System.IO;
using LanMountainDesktop.AirAppSdk;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VoiceHubLanDesktop.Services;
using VoiceHubLanDesktop.ViewModels;
using VoiceHubLanDesktop.Views;
using VoiceHubLanDesktop.Widgets;

namespace VoiceHubLanDesktop;

[AirAppEntrance]
public sealed class Plugin : AirAppBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(provider =>
        {
            var runtimeContext = provider.GetRequiredService<IAirAppRuntimeContext>();
            Directory.CreateDirectory(runtimeContext.DataDirectory);
            return new VoiceHubSettingsService(runtimeContext.DataDirectory);
        });

        services.AddSingleton<VoiceHubDataService>();
        services.AddTransient<VoiceHubSettingsViewModel>();

        services.AddAirAppComponent<VoiceHubPlaylistWidget>(
            "voicehub-playlist",
            "声动校园歌单",
            options =>
            {
                options.Description = "显示声动校园广播点歌单";
                options.DefaultWidth = 3;
                options.DefaultHeight = 4;
                options.ResizeMode = AirAppComponentResizeMode.Both;
                options.Category = "娱乐";
                options.IconKey = "MusicNote";
            });
    }

    public override Task OnStartedAsync(IAirAppRuntimeContext context)
    {
        context.Logger.Info("VoiceHub AirApp started successfully!");
        return Task.CompletedTask;
    }

    public override Task OnStoppingAsync()
    {
        return Task.CompletedTask;
    }
}

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VoiceHubLanDesktop.Services;

public sealed class VoiceHubSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [JsonPropertyName("apiUrl")]
    public string ApiUrl { get; set; } = "https://voicehub.lao-shui.top/api/songs/public";

    [JsonPropertyName("refreshIntervalMinutes")]
    public int RefreshIntervalMinutes { get; set; } = 60;

    [JsonPropertyName("showRequester")]
    public bool ShowRequester { get; set; } = true;

    [JsonPropertyName("maxDisplayCount")]
    public int MaxDisplayCount { get; set; } = 10;

    public static VoiceHubSettings Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            return new VoiceHubSettings();
        }

        try
        {
            var json = File.ReadAllText(filePath).TrimStart('\uFEFF');
            var settings = JsonSerializer.Deserialize<VoiceHubSettings>(json, JsonOptions);
            return settings ?? new VoiceHubSettings();
        }
        catch
        {
            return new VoiceHubSettings();
        }
    }

    public void Save(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(filePath, json);
        }
        catch
        {
        }
    }
}

public sealed class VoiceHubSettingsService
{
    private readonly string _settingsPath;
    private VoiceHubSettings _settings;
    private readonly object _lock = new();

    public event EventHandler<VoiceHubSettings>? SettingsChanged;

    public VoiceHubSettingsService(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        _settingsPath = Path.Combine(dataDirectory, "settings.json");
        _settings = VoiceHubSettings.Load(_settingsPath);
    }

    public VoiceHubSettings GetSettings()
    {
        lock (_lock)
        {
            return _settings;
        }
    }

    public void UpdateSettings(Action<VoiceHubSettings> updateAction)
    {
        ArgumentNullException.ThrowIfNull(updateAction);

        lock (_lock)
        {
            updateAction(_settings);
            _settings.Save(_settingsPath);
        }

        SettingsChanged?.Invoke(this, _settings);
    }

    public void UpdateApiUrl(string apiUrl)
    {
        UpdateSettings(s => s.ApiUrl = apiUrl);
    }

    public void UpdateRefreshInterval(int minutes)
    {
        UpdateSettings(s => s.RefreshIntervalMinutes = Math.Clamp(minutes, 1, 1440));
    }

    public void UpdateShowRequester(bool show)
    {
        UpdateSettings(s => s.ShowRequester = show);
    }

    public void UpdateMaxDisplayCount(int count)
    {
        UpdateSettings(s => s.MaxDisplayCount = Math.Clamp(count, 1, 50));
    }
}

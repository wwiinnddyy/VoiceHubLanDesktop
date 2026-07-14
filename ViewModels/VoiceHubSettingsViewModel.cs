using System;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using FluentIcons.Common;
using LanMountainDesktop.PluginSdk;
using VoiceHubLanDesktop.Services;
using VoiceHubLanDesktop.Widgets;

namespace VoiceHubLanDesktop.ViewModels;

public sealed class VoiceHubSettingsViewModel : INotifyPropertyChanged
{
    private readonly VoiceHubSettingsService _settingsService;
    private readonly HttpClient _httpClient;
    private readonly PluginLocalizer? _localizer;
    private readonly VoiceHubDataService _dataService;

    // Original values for reset functionality
    private string _originalApiUrl = string.Empty;
    private int _originalRefreshInterval;
    private bool _originalShowRequester;
    private int _originalMaxDisplayCount;

    // Current values
    private string _apiUrl = string.Empty;
    private int _refreshIntervalMinutes = 60;
    private bool _showRequester = true;
    private int _maxDisplayCount = 10;

    // Connection test status
    private bool _showConnectionStatus;
    private bool _isConnectionSuccess;
    private string _connectionStatusText = string.Empty;
    private Symbol _connectionStatusIcon;
    private IBrush _connectionStatusColor = Brushes.Transparent;

    public VoiceHubSettingsViewModel(
        VoiceHubSettingsService settingsService,
        VoiceHubDataService dataService,
        PluginLocalizer? localizer = null)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        _localizer = localizer;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        LoadSettings();
        SaveOriginalValues();
    }

    #region Localization Properties

    public string PageTitle => T("settingsview.page_title", "声动校园设置");
    public string PageDescription => T("settingsview.page_description", "配置 VoiceHub 歌单插件的连接和显示选项");

    public string ConnectionSectionTitle => T("settingsview.connection_title", "连接设置");
    public string ConnectionSectionDescription => T("settingsview.connection_desc", "配置与 VoiceHub 服务器的连接参数");

    public string DisplaySectionTitle => T("settingsview.display_title", "显示设置");
    public string DisplaySectionDescription => T("settingsview.display_desc", "自定义歌单在组件中的显示方式");

    public string RefreshSectionTitle => T("settingsview.refresh_title", "刷新设置");
    public string RefreshSectionDescription => T("settingsview.refresh_desc", "配置数据自动刷新的频率");

    public string ApiUrlLabel => T("settings.api_url", "API 地址");
    public string ApiUrlDescription => T("settings.api_url_desc", "VoiceHub 广播站 API 地址，用于获取歌单数据。");

    public string ShowRequesterLabel => T("settings.show_requester", "显示点歌人");
    public string ShowRequesterDescription => T("settings.show_requester_desc", "在歌曲信息中显示点歌人名称。");

    public string MaxDisplayCountLabel => T("settings.max_display_count", "最大显示数量");
    public string MaxDisplayCountDescription => T("settings.max_display_count_desc", "组件中最多显示的歌曲数量，范围 1-50。");

    public string RefreshIntervalLabel => T("settings.refresh_interval", "刷新间隔（分钟）");
    public string RefreshIntervalDescription => T("settings.refresh_interval_desc", "自动刷新歌单数据的时间间隔，范围 1-1440 分钟。");

    public string TestConnectionText => T("settingsview.test_connection", "测试连接");
    public string SaveText => T("settingsview.save", "保存设置");
    public string ResetText => T("settingsview.reset", "重置");

    #endregion

    #region Settings Properties

    public string ApiUrl
    {
        get => _apiUrl;
        set
        {
            if (_apiUrl != value)
            {
                _apiUrl = value;
                OnPropertyChanged();
                HideConnectionStatus();
            }
        }
    }

    public int RefreshIntervalMinutes
    {
        get => _refreshIntervalMinutes;
        set
        {
            if (_refreshIntervalMinutes != value)
            {
                _refreshIntervalMinutes = Math.Clamp(value, 1, 1440);
                OnPropertyChanged();
            }
        }
    }

    public bool ShowRequester
    {
        get => _showRequester;
        set
        {
            if (_showRequester != value)
            {
                _showRequester = value;
                OnPropertyChanged();
            }
        }
    }

    public int MaxDisplayCount
    {
        get => _maxDisplayCount;
        set
        {
            if (_maxDisplayCount != value)
            {
                _maxDisplayCount = Math.Clamp(value, 1, 50);
                OnPropertyChanged();
            }
        }
    }

    #endregion

    #region Connection Status Properties

    public bool ShowConnectionStatus
    {
        get => _showConnectionStatus;
        private set
        {
            if (_showConnectionStatus != value)
            {
                _showConnectionStatus = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsConnectionSuccess
    {
        get => _isConnectionSuccess;
        private set
        {
            if (_isConnectionSuccess != value)
            {
                _isConnectionSuccess = value;
                OnPropertyChanged();
            }
        }
    }

    public string ConnectionStatusText
    {
        get => _connectionStatusText;
        private set
        {
            if (_connectionStatusText != value)
            {
                _connectionStatusText = value;
                OnPropertyChanged();
            }
        }
    }

    public Symbol ConnectionStatusIcon
    {
        get => _connectionStatusIcon;
        private set
        {
            if (_connectionStatusIcon != value)
            {
                _connectionStatusIcon = value;
                OnPropertyChanged();
            }
        }
    }

    public IBrush ConnectionStatusColor
    {
        get => _connectionStatusColor;
        private set
        {
            if (!Equals(_connectionStatusColor, value))
            {
                _connectionStatusColor = value;
                OnPropertyChanged();
            }
        }
    }

    #endregion

    #region Methods

    private void LoadSettings()
    {
        var settings = _settingsService.GetSettings();
        _apiUrl = settings.ApiUrl;
        _refreshIntervalMinutes = settings.RefreshIntervalMinutes;
        _showRequester = settings.ShowRequester;
        _maxDisplayCount = settings.MaxDisplayCount;
    }

    private void SaveOriginalValues()
    {
        _originalApiUrl = _apiUrl;
        _originalRefreshInterval = _refreshIntervalMinutes;
        _originalShowRequester = _showRequester;
        _originalMaxDisplayCount = _maxDisplayCount;
    }

    public void SaveSettings()
    {
        _settingsService.UpdateSettings(settings =>
        {
            settings.ApiUrl = _apiUrl;
            settings.RefreshIntervalMinutes = _refreshIntervalMinutes;
            settings.ShowRequester = _showRequester;
            settings.MaxDisplayCount = _maxDisplayCount;
        });

        SaveOriginalValues();
        _dataService.RequestRefresh();
    }

    public void ResetToOriginal()
    {
        ApiUrl = _originalApiUrl;
        RefreshIntervalMinutes = _originalRefreshInterval;
        ShowRequester = _originalShowRequester;
        MaxDisplayCount = _originalMaxDisplayCount;
        HideConnectionStatus();
    }

    public async Task TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiUrl))
        {
            ShowConnectionError(T("settingsview.error_empty_url", "API 地址不能为空"));
            return;
        }

        if (!Uri.TryCreate(_apiUrl, UriKind.Absolute, out _) ||
            !(_apiUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
              _apiUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            ShowConnectionError(T("settingsview.error_invalid_url", "请输入有效的 HTTP/HTTPS URL"));
            return;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var response = await _httpClient.GetAsync(_apiUrl, cts.Token);

            if (response.IsSuccessStatusCode)
            {
                ShowConnectionSuccess(T("settingsview.connection_success", "连接成功"));
            }
            else
            {
                ShowConnectionError(string.Format(
                    T("settingsview.connection_failed", "连接失败 (HTTP {0})"),
                    (int)response.StatusCode));
            }
        }
        catch (OperationCanceledException)
        {
            ShowConnectionError(T("settingsview.connection_timeout", "连接超时"));
        }
        catch (HttpRequestException ex)
        {
            ShowConnectionError(string.Format(
                T("settingsview.connection_error", "连接错误: {0}"),
                ex.Message));
        }
        catch (Exception ex)
        {
            ShowConnectionError(string.Format(
                T("settingsview.connection_error", "连接错误: {0}"),
                ex.Message));
        }
    }

    private void ShowConnectionSuccess(string message)
    {
        IsConnectionSuccess = true;
        ConnectionStatusText = message;
        ConnectionStatusIcon = Symbol.CheckmarkCircle;
        ConnectionStatusColor = SolidColorBrush.Parse("#2E7D32");
        ShowConnectionStatus = true;
    }

    private void ShowConnectionError(string message)
    {
        IsConnectionSuccess = false;
        ConnectionStatusText = message;
        ConnectionStatusIcon = Symbol.DismissCircle;
        ConnectionStatusColor = SolidColorBrush.Parse("#D32F2F");
        ShowConnectionStatus = true;
    }

    private void HideConnectionStatus()
    {
        ShowConnectionStatus = false;
    }

    private string T(string key, string fallback)
    {
        return _localizer?.GetString(key, fallback) ?? fallback;
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using LanMountainDesktop.PluginSdk;
using VoiceHubLanDesktop.Models;
using VoiceHubLanDesktop.Services;

namespace VoiceHubLanDesktop.Widgets;

public partial class VoiceHubPlaylistWidget : UserControl
{
    private readonly PluginDesktopComponentContext _context;
    private readonly PluginLocalizer _localizer;
    private readonly VoiceHubSettingsService _settingsService;
    private readonly VoiceHubDataService _dataService;
    private readonly IPluginMessageBus? _messageBus;

    private readonly HttpClient _httpClient = new();
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly DispatcherTimer _refreshTimer;

    private ComponentState _currentState = ComponentState.Loading;
    private List<SongItem> _currentSongs = [];
    private DateTime? _displayDate;

    private readonly List<IDisposable> _subscriptions = [];

    public VoiceHubPlaylistWidget()
    {
        InitializeComponent();
    }

    public VoiceHubPlaylistWidget(
        PluginDesktopComponentContext context,
        VoiceHubSettingsService settingsService,
        VoiceHubDataService dataService) : this()
    {
        _context = context;
        _localizer = PluginLocalizer.Create(context);
        _settingsService = settingsService;
        _dataService = dataService;
        _messageBus = context.GetService<IPluginMessageBus>();

        _httpClient.Timeout = TimeSpan.FromSeconds(10);

        var settings = _settingsService.GetSettings();
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(settings.RefreshIntervalMinutes)
        };
        _refreshTimer.Tick += async (_, _) => await RefreshAsync();

        SetupBackground();
        SetState(ComponentState.Loading);

        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        SizeChanged += OnSizeChanged;

        _settingsService.SettingsChanged += OnSettingsChanged;
    }

    private void SetupBackground()
    {
        Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(Color.Parse("#FF07111F"), 0),
                new GradientStop(Color.Parse("#FF0C4A6E"), 0.55),
                new GradientStop(Color.Parse("#FF0EA5E9"), 1)
            ]
        };
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        SubscribeToPluginBus();
        _ = RefreshAsync();
        _refreshTimer.Start();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _refreshTimer.Stop();
        _cancellationTokenSource?.Cancel();

        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }
        _subscriptions.Clear();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        ApplyScale();
    }

    private void OnSettingsChanged(object? sender, VoiceHubSettings settings)
    {
        _refreshTimer.Interval = TimeSpan.FromMinutes(settings.RefreshIntervalMinutes);
        Dispatcher.UIThread.Post(async () => await RefreshAsync());
    }

    private void SubscribeToPluginBus()
    {
        if (_messageBus is null || _subscriptions.Count > 0)
        {
            return;
        }

        _subscriptions.Add(_messageBus.Subscribe<VoiceHubDataRefreshMessage>(_ =>
            Dispatcher.UIThread.Post(async () => await RefreshAsync())));
    }

    public async Task RefreshAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            var settings = _settingsService.GetSettings();
            var apiUrl = string.IsNullOrWhiteSpace(settings.ApiUrl)
                ? "https://voicehub.lao-shui.top/api/songs/public"
                : settings.ApiUrl;

            var jsonResponse = await _httpClient.GetStringAsync(apiUrl, _cancellationTokenSource.Token);
            var songItems = JsonSerializer.Deserialize<List<SongItem>>(jsonResponse);

            if (songItems == null || !songItems.Any())
            {
                SetState(ComponentState.NoSchedule);
                return;
            }

            var validItems = songItems.Where(s => s.GetPlayDate() != DateTime.MinValue).ToList();

            if (!validItems.Any())
            {
                SetState(ComponentState.NoSchedule);
                return;
            }

            var today = DateTime.Today;
            var todaySchedule = validItems.Where(s => s.GetPlayDate() == today).OrderBy(s => s.Sequence).ToList();

            List<SongItem> displayItems;
            DateTime actualDate;

            if (todaySchedule.Any())
            {
                displayItems = todaySchedule;
                actualDate = today;
            }
            else
            {
                var futureSchedule = validItems
                    .Where(s => s.GetPlayDate() > today)
                    .GroupBy(s => s.GetPlayDate())
                    .OrderBy(g => g.Key)
                    .FirstOrDefault();

                if (futureSchedule != null)
                {
                    displayItems = futureSchedule.OrderBy(s => s.Sequence).ToList();
                    actualDate = futureSchedule.Key;
                }
                else
                {
                    SetState(ComponentState.NoSchedule);
                    return;
                }
            }

            _currentSongs = displayItems;
            _displayDate = actualDate;

            SetState(ComponentState.Normal);
            UpdateSongsPanel();
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException)
        {
            SetState(ComponentState.NetworkError, T("error.network", "网络连接错误"));
        }
        catch (JsonException)
        {
            SetState(ComponentState.NetworkError, T("error.data_format", "数据格式错误"));
        }
        catch (Exception)
        {
            SetState(ComponentState.NetworkError, T("error.unknown", "获取失败"));
        }
    }

    private void SetState(ComponentState state, string? errorMessage = null)
    {
        _currentState = state;

        Dispatcher.UIThread.Post(() =>
        {
            SongsScrollViewer.IsVisible = false;
            LoadingHost.IsVisible = false;
            ErrorHost.IsVisible = false;

            switch (state)
            {
                case ComponentState.Loading:
                    LoadingHost.IsVisible = true;
                    LoadingText.Text = T("status.loading", "正在加载...");
                    HeaderText.Text = T("header.title", "声动校园歌单");
                    DateText.Text = "";
                    break;

                case ComponentState.Normal:
                    SongsScrollViewer.IsVisible = true;
                    HeaderText.Text = T("header.title", "声动校园歌单");
                    DateText.Text = _displayDate?.ToString("MM/dd") ?? "";
                    break;

                case ComponentState.NetworkError:
                    ErrorHost.IsVisible = true;
                    ErrorText.Text = errorMessage ?? T("error.network", "网络错误");
                    HeaderText.Text = T("header.title", "声动校园歌单");
                    DateText.Text = "";
                    break;

                case ComponentState.NoSchedule:
                    ErrorHost.IsVisible = true;
                    ErrorText.Text = T("status.no_schedule", "暂无排期数据");
                    HeaderText.Text = T("header.title", "声动校园歌单");
                    DateText.Text = "";
                    break;
            }

            ApplyScale();
        });
    }

    private void UpdateSongsPanel()
    {
        Dispatcher.UIThread.Post(() =>
        {
            SongsPanel.Children.Clear();

            var settings = _settingsService.GetSettings();
            var songs = _currentSongs.Take(settings.MaxDisplayCount).ToList();
            var basis = GetLayoutBasis();

            var titleSize = Math.Clamp(basis * 0.055, 11, 15);
            var detailSize = Math.Clamp(basis * 0.045, 9, 12);

            foreach (var item in songs)
            {
                var song = item.Song;
                var songCard = CreateSongCard(item, song, titleSize, detailSize, basis);
                SongsPanel.Children.Add(songCard);
            }

            if (_currentSongs.Count > settings.MaxDisplayCount)
            {
                SongsPanel.Children.Add(new TextBlock
                {
                    Text = T("status.more", "还有 {0} 首...", _currentSongs.Count - settings.MaxDisplayCount),
                    FontSize = detailSize,
                    Foreground = new SolidColorBrush(Color.Parse("#FF93C5FD")),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 8, 0, 0)
                });
            }
        });
    }

    private Border CreateSongCard(SongItem item, Song song, double titleSize, double detailSize, double basis)
    {
        var sequenceBorder = new Border
        {
            Width = Math.Clamp(basis * 0.06, 18, 28),
            Height = Math.Clamp(basis * 0.06, 18, 28),
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Color.Parse("#FF38BDF8")),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = $"#{item.Sequence}",
                FontSize = detailSize,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        var infoPanel = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center
        };

        infoPanel.Children.Add(new TextBlock
        {
            Text = $"{song.Artist} - {song.Title}",
            FontSize = titleSize,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            MaxLines = 2,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        var settings = _settingsService.GetSettings();
        if (settings.ShowRequester)
        {
            infoPanel.Children.Add(new TextBlock
            {
                Text = T("song.requester", "点歌：{0}", song.Requester),
                FontSize = detailSize,
                Foreground = new SolidColorBrush(Color.Parse("#FFBAE6FD")),
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 1,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        }

        var voteText = new TextBlock
        {
            Text = song.VoteCount > 0 ? $"🔥{song.VoteCount}" : "",
            FontSize = detailSize,
            Foreground = new SolidColorBrush(Color.Parse("#FFFBBF24")),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var contentGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 8
        };

        contentGrid.Children.Add(sequenceBorder);
        contentGrid.Children.Add(infoPanel);
        contentGrid.Children.Add(voteText);
        Grid.SetColumn(infoPanel, 1);
        Grid.SetColumn(voteText, 2);

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1F082F49")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3338BDF8")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 6),
            Child = contentGrid
        };
    }

    private void ApplyScale()
    {
        var basis = GetLayoutBasis();

        Padding = new Thickness(Math.Clamp(basis * 0.04, 8, 16));

        HeaderHost.Padding = new Thickness(
            Math.Clamp(basis * 0.06, 8, 14),
            Math.Clamp(basis * 0.04, 6, 10));
        HeaderText.FontSize = Math.Clamp(basis * 0.065, 13, 18);
        DateText.FontSize = Math.Clamp(basis * 0.055, 11, 15);

        SongsScrollViewer.Padding = new Thickness(
            Math.Clamp(basis * 0.04, 6, 12),
            Math.Clamp(basis * 0.02, 4, 8));
        SongsPanel.Spacing = Math.Clamp(basis * 0.025, 4, 8);

        if (_currentState == ComponentState.Normal)
        {
            UpdateSongsPanel();
        }
    }

    private double GetLayoutBasis()
    {
        var width = Bounds.Width > 1 ? Bounds.Width : _context.CellSize * 3;
        var height = Bounds.Height > 1 ? Bounds.Height : _context.CellSize * 4;
        return Math.Max(_context.CellSize * 3, Math.Min(width, height));
    }

    private string T(string key, string fallback)
    {
        return _localizer.GetString(key, fallback);
    }

    private string T(string key, string fallback, params object[] args)
    {
        return _localizer.Format(key, fallback, args);
    }
}

public sealed class VoiceHubDataRefreshMessage;

public sealed class VoiceHubDataService
{
    public event EventHandler? DataRefreshRequested;

    public void RequestRefresh()
    {
        DataRefreshRequested?.Invoke(this, EventArgs.Empty);
    }
}

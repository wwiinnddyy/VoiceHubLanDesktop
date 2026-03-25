using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using FluentIcons.Avalonia;
using FluentIcons.Common;
using LanMountainDesktop.PluginSdk;
using VoiceHubLanDesktop.Models;
using VoiceHubLanDesktop.Services;

namespace VoiceHubLanDesktop.Widgets;

public partial class VoiceHubPlaylistWidget : UserControl
{
    private PluginDesktopComponentContext? _context;
    private PluginLocalizer? _localizer;
    private VoiceHubSettingsService? _settingsService;
    private VoiceHubDataService? _dataService;
    private IPluginMessageBus? _messageBus;

    private readonly HttpClient _httpClient = new();
    private CancellationTokenSource? _cancellationTokenSource;
    private DispatcherTimer? _refreshTimer;

    private ComponentState _currentState = ComponentState.Loading;
    private List<SongItem> _currentSongs = [];
    private DateTime? _displayDate;
    private bool _isDarkMode;

    private readonly List<IDisposable> _subscriptions = [];
    private readonly Dictionary<string, Bitmap> _coverCache = [];

    private bool _isDesignMode;

    private static class NetEaseColors
    {
        public static readonly Color LightPrimary = Color.Parse("#D43C33");
        public static readonly Color LightPrimaryDark = Color.Parse("#C20B0B");
        public static readonly Color LightBackground = Color.Parse("#FAFAFA");
        public static readonly Color LightSurface = Color.Parse("#FFFFFFFF");
        public static readonly Color LightText = Color.Parse("#333333");
        public static readonly Color LightTextSecondary = Color.Parse("#666666");
        public static readonly Color LightBorder = Color.Parse("#E5E5E5");

        public static readonly Color DarkPrimary = Color.Parse("#D43C33");
        public static readonly Color DarkPrimaryLight = Color.Parse("#E85A52");
        public static readonly Color DarkBackground = Color.Parse("#1A1A1A");
        public static readonly Color DarkSurface = Color.Parse("#2A2A2A");
        public static readonly Color DarkSurfaceLight = Color.Parse("#333333");
        public static readonly Color DarkText = Color.Parse("#F5F5F5");
        public static readonly Color DarkTextSecondary = Color.Parse("#B3B3B3");
        public static readonly Color DarkBorder = Color.Parse("#3D3D3D");

        public static readonly Color Accent = Color.Parse("#FF6666");
        public static readonly Color VoteHot = Color.Parse("#FF6B35");
    }

    public VoiceHubPlaylistWidget()
    {
        InitializeComponent();

        _isDesignMode = Design.IsDesignMode;
        if (_isDesignMode)
        {
            SetupDesignTimePreview();
        }
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

        _isDarkMode = ResolveIsDarkMode();
        ApplyTheme();

        SetState(ComponentState.Loading);

        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        SizeChanged += OnSizeChanged;
        ActualThemeVariantChanged += OnThemeVariantChanged;

        _settingsService.SettingsChanged += OnSettingsChanged;
    }

    private void SetupDesignTimePreview()
    {
        Width = 300;
        Height = 400;

        _isDarkMode = false;
        ApplyDesignTimeTheme();

        _currentSongs =
        [
            new SongItem
            {
                Sequence = 1,
                Song = new Song
                {
                    Title = "晴天",
                    Artist = "周杰伦",
                    Requester = "张三",
                    VoteCount = 5,
                    Cover = null
                }
            },
            new SongItem
            {
                Sequence = 2,
                Song = new Song
                {
                    Title = "七里香",
                    Artist = "周杰伦",
                    Requester = "李四",
                    VoteCount = 3,
                    Cover = null
                }
            },
            new SongItem
            {
                Sequence = 3,
                Song = new Song
                {
                    Title = "稻香",
                    Artist = "周杰伦",
                    Requester = "王五",
                    VoteCount = 8,
                    Cover = null
                }
            }
        ];
        _displayDate = DateTime.Today;

        SetState(ComponentState.Normal);
        UpdateSongsPanel();
    }

    private void ApplyDesignTimeTheme()
    {
        var cornerRadius = 16d;

        Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(Color.Parse("#FFFFFFFF"), 0),
                new GradientStop(Color.Parse("#FFFAFAFA"), 0.5),
                new GradientStop(Color.Parse("#FFF5F5F5"), 1)
            ]
        };

        RootBorder.CornerRadius = new CornerRadius(cornerRadius);
        RootBorder.BorderBrush = new SolidColorBrush(NetEaseColors.LightBorder);

        HeaderHost.Background = new SolidColorBrush(Color.Parse("#10D43C33"));
        HeaderIcon.Foreground = new SolidColorBrush(NetEaseColors.LightPrimary);
        HeaderText.Foreground = new SolidColorBrush(NetEaseColors.LightText);
        HeaderText.Text = "声动校园歌单";
        DateText.Foreground = new SolidColorBrush(NetEaseColors.LightTextSecondary);
        DateText.Text = DateTime.Today.ToString("MM/dd");

        SongsScrollViewer.Background = Brushes.Transparent;
    }

    private bool ResolveIsDarkMode()
    {
        if (_isDesignMode || _context is null)
        {
            return false;
        }

        var themeVariant = _context.Appearance.Snapshot.ThemeVariant;
        if (string.Equals(themeVariant, "Dark", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(themeVariant, "Light", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (ActualThemeVariant == ThemeVariant.Dark)
        {
            return true;
        }

        if (ActualThemeVariant == ThemeVariant.Light)
        {
            return false;
        }

        return Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
    }

    private void OnThemeVariantChanged(object? sender, EventArgs e)
    {
        if (_isDesignMode) return;

        var newIsDarkMode = ResolveIsDarkMode();
        if (_isDarkMode != newIsDarkMode)
        {
            _isDarkMode = newIsDarkMode;
            ApplyTheme();
            UpdateSongsPanel();
        }
    }

    private void ApplyTheme()
    {
        if (_isDesignMode || _context is null) return;

        var cornerRadius = _context.ResolveCornerRadius(PluginCornerRadiusPreset.Lg);
        RootBorder.CornerRadius = new CornerRadius(cornerRadius);

        if (_isDarkMode)
        {
            ApplyDarkTheme(cornerRadius);
        }
        else
        {
            ApplyLightTheme(cornerRadius);
        }
    }

    private void ApplyLightTheme(double cornerRadius)
    {
        Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(Color.Parse("#FFFFFFFF"), 0),
                new GradientStop(Color.Parse("#FFFAFAFA"), 0.5),
                new GradientStop(Color.Parse("#FFF5F5F5"), 1)
            ]
        };

        RootBorder.Background = null;
        RootBorder.BorderBrush = new SolidColorBrush(NetEaseColors.LightBorder);

        HeaderHost.Background = new SolidColorBrush(Color.Parse("#10D43C33"));
        HeaderIcon.Foreground = new SolidColorBrush(NetEaseColors.LightPrimary);
        HeaderText.Foreground = new SolidColorBrush(NetEaseColors.LightText);
        DateText.Foreground = new SolidColorBrush(NetEaseColors.LightTextSecondary);

        SongsScrollViewer.Background = Brushes.Transparent;

        LoadingHost.Background = new SolidColorBrush(Color.Parse("#10D43C33"));
        LoadingHost.CornerRadius = new CornerRadius(cornerRadius);
        LoadingText.Foreground = new SolidColorBrush(NetEaseColors.LightTextSecondary);

        ErrorHost.Background = new SolidColorBrush(Color.Parse("#10FF6B35"));
        ErrorHost.BorderBrush = new SolidColorBrush(Color.Parse("#30FF6B35"));
        ErrorHost.CornerRadius = new CornerRadius(cornerRadius);
        ErrorIcon.Foreground = new SolidColorBrush(NetEaseColors.VoteHot);
        ErrorText.Foreground = new SolidColorBrush(NetEaseColors.LightText);
    }

    private void ApplyDarkTheme(double cornerRadius)
    {
        Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(Color.Parse("#FF1A1A1A"), 0),
                new GradientStop(Color.Parse("#FF222222"), 0.5),
                new GradientStop(Color.Parse("#FF2A2A2A"), 1)
            ]
        };

        RootBorder.Background = null;
        RootBorder.BorderBrush = new SolidColorBrush(NetEaseColors.DarkBorder);

        HeaderHost.Background = new SolidColorBrush(Color.Parse("#20D43C33"));
        HeaderIcon.Foreground = new SolidColorBrush(NetEaseColors.DarkPrimary);
        HeaderText.Foreground = new SolidColorBrush(NetEaseColors.DarkText);
        DateText.Foreground = new SolidColorBrush(NetEaseColors.DarkTextSecondary);

        SongsScrollViewer.Background = Brushes.Transparent;

        LoadingHost.Background = new SolidColorBrush(Color.Parse("#20D43C33"));
        LoadingHost.CornerRadius = new CornerRadius(cornerRadius);
        LoadingText.Foreground = new SolidColorBrush(NetEaseColors.DarkTextSecondary);

        ErrorHost.Background = new SolidColorBrush(Color.Parse("#20FF6B35"));
        ErrorHost.BorderBrush = new SolidColorBrush(Color.Parse("#30FF6B35"));
        ErrorHost.CornerRadius = new CornerRadius(cornerRadius);
        ErrorIcon.Foreground = new SolidColorBrush(NetEaseColors.VoteHot);
        ErrorText.Foreground = new SolidColorBrush(NetEaseColors.DarkText);
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_isDesignMode) return;

        SubscribeToPluginBus();
        _isDarkMode = ResolveIsDarkMode();
        ApplyTheme();
        _ = RefreshAsync();
        _refreshTimer?.Start();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_isDesignMode) return;

        _refreshTimer?.Stop();
        _cancellationTokenSource?.Cancel();

        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }
        _subscriptions.Clear();

        foreach (var bitmap in _coverCache.Values)
        {
            bitmap.Dispose();
        }
        _coverCache.Clear();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        ApplyScale();
    }

    private void OnSettingsChanged(object? sender, VoiceHubSettings settings)
    {
        if (_refreshTimer is null) return;

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
        if (_settingsService is null) return;

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

            var settings = _settingsService?.GetSettings() ?? new VoiceHubSettings { MaxDisplayCount = 10, ShowRequester = true };
            var songs = _currentSongs.Take(settings.MaxDisplayCount).ToList();
            var basis = GetLayoutBasis();

            var titleSize = Math.Clamp(basis * 0.055, 11, 15);
            var detailSize = Math.Clamp(basis * 0.045, 9, 12);

            foreach (var item in songs)
            {
                var song = item.Song;
                var songCard = CreateSongCard(song, titleSize, detailSize, basis, settings);
                SongsPanel.Children.Add(songCard);
            }

            if (_currentSongs.Count > settings.MaxDisplayCount)
            {
                SongsPanel.Children.Add(new TextBlock
                {
                    Text = T("status.more", "还有 {0} 首...", _currentSongs.Count - settings.MaxDisplayCount),
                    FontSize = detailSize,
                    Foreground = _isDarkMode
                        ? new SolidColorBrush(NetEaseColors.DarkTextSecondary)
                        : new SolidColorBrush(NetEaseColors.LightTextSecondary),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 8, 0, 0)
                });
            }
        });
    }

    private Border CreateSongCard(Song song, double titleSize, double detailSize, double basis, VoiceHubSettings? settings = null)
    {
        var cardCornerRadius = _isDesignMode ? 8d : _context!.ResolveCornerRadius(PluginCornerRadiusPreset.Sm);

        var surfaceColor = _isDarkMode ? NetEaseColors.DarkSurface : Color.Parse("#FFF8F8F8");
        var textColor = _isDarkMode ? NetEaseColors.DarkText : NetEaseColors.LightText;
        var textSecondaryColor = _isDarkMode ? NetEaseColors.DarkTextSecondary : NetEaseColors.LightTextSecondary;
        var borderColor = _isDarkMode ? NetEaseColors.DarkBorder : Color.Parse("#FFE8E8E8");

        var coverSize = Math.Clamp(basis * 0.095, 36, 52);
        var coverBorder = new Border
        {
            Width = coverSize,
            Height = coverSize,
            CornerRadius = new CornerRadius(cardCornerRadius * 0.6),
            Background = new SolidColorBrush(_isDarkMode ? NetEaseColors.DarkSurfaceLight : Color.Parse("#FFE8E8E8")),
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Center
        };

        var fallbackIcon = new SymbolIcon
        {
            Symbol = Symbol.MusicNote1,
            IconVariant = IconVariant.Regular,
            FontSize = coverSize * 0.5,
            Foreground = new SolidColorBrush(_isDarkMode ? NetEaseColors.DarkTextSecondary : NetEaseColors.LightTextSecondary),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        coverBorder.Child = fallbackIcon;

        if (!string.IsNullOrWhiteSpace(song.Cover))
        {
            _ = LoadCoverAsync(song.Cover, coverBorder, coverSize);
        }

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
            Foreground = new SolidColorBrush(textColor),
            TextWrapping = TextWrapping.Wrap,
            MaxLines = 2,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        var effectiveSettings = settings ?? _settingsService?.GetSettings() ?? new VoiceHubSettings { MaxDisplayCount = 10, ShowRequester = true };
        if (effectiveSettings.ShowRequester)
        {
            infoPanel.Children.Add(new TextBlock
            {
                Text = T("song.requester", "点歌：{0}", song.Requester),
                FontSize = detailSize,
                Foreground = new SolidColorBrush(textSecondaryColor),
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 1,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        }

        var voteText = new TextBlock
        {
            Text = song.VoteCount > 0 ? $"🔥{song.VoteCount}" : "",
            FontSize = detailSize,
            Foreground = new SolidColorBrush(NetEaseColors.VoteHot),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var contentGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 8
        };

        contentGrid.Children.Add(coverBorder);
        contentGrid.Children.Add(infoPanel);
        contentGrid.Children.Add(voteText);
        Grid.SetColumn(infoPanel, 1);
        Grid.SetColumn(voteText, 2);

        return new Border
        {
            Background = new SolidColorBrush(surfaceColor),
            BorderBrush = new SolidColorBrush(borderColor),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(cardCornerRadius),
            Padding = new Thickness(8, 6),
            Child = contentGrid
        };
    }

    private async Task LoadCoverAsync(string coverUrl, Border coverBorder, double coverSize)
    {
        if (_coverCache.TryGetValue(coverUrl, out var cachedBitmap))
        {
            Dispatcher.UIThread.Post(() =>
            {
                coverBorder.Child = new Image
                {
                    Source = cachedBitmap,
                    Stretch = Stretch.UniformToFill
                };
            });
            return;
        }

        try
        {
            var imageBytes = await _httpClient.GetByteArrayAsync(coverUrl);
            using var stream = new MemoryStream(imageBytes);
            var bitmap = new Bitmap(stream);

            _coverCache[coverUrl] = bitmap;

            Dispatcher.UIThread.Post(() =>
            {
                coverBorder.Child = new Image
                {
                    Source = bitmap,
                    Stretch = Stretch.UniformToFill
                };
            });
        }
        catch
        {
        }
    }

    private void ApplyScale()
    {
        var basis = GetLayoutBasis();
        var cornerRadius = _isDesignMode ? 16d : _context!.ResolveCornerRadius(PluginCornerRadiusPreset.Lg);

        RootBorder.CornerRadius = new CornerRadius(cornerRadius);

        Padding = new Thickness(Math.Clamp(basis * 0.04, 8, 16));

        HeaderHost.Padding = new Thickness(
            Math.Clamp(basis * 0.06, 8, 14),
            Math.Clamp(basis * 0.04, 6, 10));
        HeaderText.FontSize = Math.Clamp(basis * 0.065, 13, 18);
        HeaderIcon.FontSize = Math.Clamp(basis * 0.055, 12, 18);
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
        if (_isDesignMode)
        {
            return Math.Min(Bounds.Width, Bounds.Height);
        }

        var width = Bounds.Width > 1 ? Bounds.Width : _context!.CellSize * 3;
        var height = Bounds.Height > 1 ? Bounds.Height : _context!.CellSize * 4;
        return Math.Max(_context!.CellSize * 3, Math.Min(width, height));
    }

    private string T(string key, string fallback)
    {
        return _localizer?.GetString(key, fallback) ?? fallback;
    }

    private string T(string key, string fallback, params object[] args)
    {
        return _localizer?.Format(key, fallback, args) ?? string.Format(fallback, args);
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

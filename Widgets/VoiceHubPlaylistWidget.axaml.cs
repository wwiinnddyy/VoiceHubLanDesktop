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
    private ISettingsService? _globalSettingsService;

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
    private double _lastKnownCornerRadiusScale = 1.0;

    private static class ThemeColors
    {
        public static readonly Color LightCardBackground = Color.Parse("#FCFBFA");
        public static readonly Color LightCardBorder = Color.Parse("#E8E8E8");
        public static readonly Color LightText = Color.Parse("#2B2F35");
        public static readonly Color LightTextSecondary = Color.Parse("#7A8088");
        public static readonly Color LightIconBadgeBackground = Color.Parse("#14D43C33");
        public static readonly Color LightIconBadgeBorder = Color.Parse("#20D43C33");
        public static readonly Color LightRefreshButtonBackground = Color.Parse("#14A0A6AF");

        public static readonly Color DarkCardBackground = Color.Parse("#1B2129");
        public static readonly Color DarkCardBorder = Color.Parse("#2D3440");
        public static readonly Color DarkText = Color.Parse("#E8EAED");
        public static readonly Color DarkTextSecondary = Color.Parse("#A8B1C2");
        public static readonly Color DarkIconBadgeBackground = Color.Parse("#2D3440");
        public static readonly Color DarkIconBadgeBorder = Color.Parse("#3D4450");
        public static readonly Color DarkRefreshButtonBackground = Color.Parse("#2D3440");

        public static readonly Color Primary = Color.Parse("#D43C33");
        public static readonly Color Accent = Color.Parse("#FF6666");
        public static readonly Color Warning = Color.Parse("#FF6B35");
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
        _globalSettingsService = context.GetService<ISettingsService>();

        _httpClient.Timeout = TimeSpan.FromSeconds(10);

        var settings = _settingsService.GetSettings();
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(settings.RefreshIntervalMinutes)
        };
        _refreshTimer.Tick += async (_, _) => await RefreshAsync();

        _isDarkMode = ResolveIsDarkMode();
        _lastKnownCornerRadiusScale = context.GlobalCornerRadiusScale;
        ApplyTheme();

        SetState(ComponentState.Loading);

        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        SizeChanged += OnSizeChanged;
        ActualThemeVariantChanged += OnThemeVariantChanged;

        _settingsService.SettingsChanged += OnSettingsChanged;

        if (_globalSettingsService is not null)
        {
            _globalSettingsService.Changed += OnGlobalSettingsChanged;
        }
    }

    private void SetupDesignTimePreview()
    {
        Width = 300;
        Height = 400;

        _isDarkMode = false;
        ApplyTheme();

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

        var cornerRadius = ResolveCurrentCornerRadius(PluginCornerRadiusPreset.Lg);
        RootBorder.CornerRadius = new CornerRadius(cornerRadius);
        CardBackground.CornerRadius = new CornerRadius(cornerRadius);
        CardBorder.CornerRadius = new CornerRadius(cornerRadius);

        if (_isDarkMode)
        {
            ApplyDarkTheme(cornerRadius);
        }
        else
        {
            ApplyLightTheme(cornerRadius);
        }
    }

    private double ResolveCurrentCornerRadius(PluginCornerRadiusPreset preset)
    {
        if (_context is null) return 24d;

        var baseRadius = preset switch
        {
            PluginCornerRadiusPreset.Micro => 6d,
            PluginCornerRadiusPreset.Xs => 10d,
            PluginCornerRadiusPreset.Sm => 14d,
            PluginCornerRadiusPreset.Md => 18d,
            PluginCornerRadiusPreset.Lg => 24d,
            PluginCornerRadiusPreset.Xl => 30d,
            PluginCornerRadiusPreset.Island => 36d,
            _ => 18d
        };

        return Math.Round(baseRadius * _lastKnownCornerRadiusScale * 2, MidpointRounding.AwayFromZero) / 2d;
    }

    private void ApplyLightTheme(double cornerRadius)
    {
        CardBorder.Background = new SolidColorBrush(ThemeColors.LightCardBackground);
        CardBorder.BorderBrush = new SolidColorBrush(ThemeColors.LightCardBorder);

        HeaderIconBadge.Background = new SolidColorBrush(ThemeColors.LightIconBadgeBackground);
        HeaderIconBadge.BorderBrush = new SolidColorBrush(ThemeColors.LightIconBadgeBorder);
        HeaderIcon.Foreground = new SolidColorBrush(ThemeColors.Primary);

        HeaderText.Foreground = new SolidColorBrush(ThemeColors.LightText);
        DateText.Foreground = new SolidColorBrush(ThemeColors.LightTextSecondary);

        RefreshButton.Background = new SolidColorBrush(ThemeColors.LightRefreshButtonBackground);
        RefreshIcon.Foreground = new SolidColorBrush(ThemeColors.LightTextSecondary);

        LoadingHost.Background = new SolidColorBrush(Color.Parse("#14D43C33"));
        LoadingHost.CornerRadius = new CornerRadius(cornerRadius);
        LoadingText.Foreground = new SolidColorBrush(ThemeColors.LightTextSecondary);

        ErrorHost.Background = new SolidColorBrush(Color.Parse("#14FF6B35"));
        ErrorHost.BorderBrush = new SolidColorBrush(Color.Parse("#30FF6B35"));
        ErrorHost.CornerRadius = new CornerRadius(cornerRadius);
        ErrorIcon.Foreground = new SolidColorBrush(ThemeColors.Warning);
        ErrorText.Foreground = new SolidColorBrush(ThemeColors.LightText);

        EmptyHost.Background = new SolidColorBrush(Color.Parse("#0C000000"));
        EmptyHost.CornerRadius = new CornerRadius(cornerRadius);
        EmptyText.Foreground = new SolidColorBrush(ThemeColors.LightTextSecondary);
    }

    private void ApplyDarkTheme(double cornerRadius)
    {
        CardBorder.Background = new SolidColorBrush(ThemeColors.DarkCardBackground);
        CardBorder.BorderBrush = new SolidColorBrush(ThemeColors.DarkCardBorder);

        HeaderIconBadge.Background = new SolidColorBrush(ThemeColors.DarkIconBadgeBackground);
        HeaderIconBadge.BorderBrush = new SolidColorBrush(ThemeColors.DarkIconBadgeBorder);
        HeaderIcon.Foreground = new SolidColorBrush(ThemeColors.Primary);

        HeaderText.Foreground = new SolidColorBrush(ThemeColors.DarkText);
        DateText.Foreground = new SolidColorBrush(ThemeColors.DarkTextSecondary);

        RefreshButton.Background = new SolidColorBrush(ThemeColors.DarkRefreshButtonBackground);
        RefreshIcon.Foreground = new SolidColorBrush(ThemeColors.DarkTextSecondary);

        LoadingHost.Background = new SolidColorBrush(Color.Parse("#20D43C33"));
        LoadingHost.CornerRadius = new CornerRadius(cornerRadius);
        LoadingText.Foreground = new SolidColorBrush(ThemeColors.DarkTextSecondary);

        ErrorHost.Background = new SolidColorBrush(Color.Parse("#20FF6B35"));
        ErrorHost.BorderBrush = new SolidColorBrush(Color.Parse("#30FF6B35"));
        ErrorHost.CornerRadius = new CornerRadius(cornerRadius);
        ErrorIcon.Foreground = new SolidColorBrush(ThemeColors.Warning);
        ErrorText.Foreground = new SolidColorBrush(ThemeColors.DarkText);

        EmptyHost.Background = new SolidColorBrush(Color.Parse("#1A1A1A1A"));
        EmptyHost.CornerRadius = new CornerRadius(cornerRadius);
        EmptyText.Foreground = new SolidColorBrush(ThemeColors.DarkTextSecondary);
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

        if (_globalSettingsService is not null)
        {
            _globalSettingsService.Changed -= OnGlobalSettingsChanged;
        }
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

    private void OnGlobalSettingsChanged(object? sender, SettingsChangedEvent e)
    {
        if (e.Scope != SettingsScope.App) return;

        var changedKeys = e.ChangedKeys;
        var shouldRefreshCornerRadius = changedKeys.Count == 0 ||
            changedKeys.Contains("GlobalCornerRadiusScale", StringComparer.OrdinalIgnoreCase);

        if (shouldRefreshCornerRadius && _context is not null)
        {
            var newScale = _globalSettingsService?.GetValue<double>(
                SettingsScope.App, "GlobalCornerRadiusScale") ?? 1.0;

            if (Math.Abs(newScale - _lastKnownCornerRadiusScale) > 0.001)
            {
                _lastKnownCornerRadiusScale = newScale;
                Dispatcher.UIThread.Post(() =>
                {
                    ApplyTheme();
                    ApplyScale();
                });
            }
        }
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
            EmptyHost.IsVisible = false;

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
                    EmptyHost.IsVisible = true;
                    EmptyText.Text = T("status.no_schedule", "暂无排期数据");
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
            SongsStackPanel.Children.Clear();

            var settings = _settingsService?.GetSettings() ?? new VoiceHubSettings { MaxDisplayCount = 10, ShowRequester = true };
            var songs = _currentSongs.Take(settings.MaxDisplayCount).ToList();
            var basis = GetLayoutBasis();

            var titleSize = Math.Clamp(basis * 0.042, 11, 15);
            var detailSize = Math.Clamp(basis * 0.035, 9, 12);

            foreach (var item in songs)
            {
                var song = item.Song;
                var songCard = CreateSongCard(song, titleSize, detailSize, basis, settings);
                SongsStackPanel.Children.Add(songCard);
            }

            if (_currentSongs.Count > settings.MaxDisplayCount)
            {
                SongsStackPanel.Children.Add(new TextBlock
                {
                    Text = T("status.more", "还有 {0} 首...", _currentSongs.Count - settings.MaxDisplayCount),
                    FontSize = detailSize,
                    Foreground = _isDarkMode
                        ? new SolidColorBrush(ThemeColors.DarkTextSecondary)
                        : new SolidColorBrush(ThemeColors.LightTextSecondary),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 8, 0, 0)
                });
            }
        });
    }

    private Border CreateSongCard(Song song, double titleSize, double detailSize, double basis, VoiceHubSettings? settings = null)
    {
        var cardCornerRadius = _isDesignMode ? 10d : ResolveCurrentCornerRadius(PluginCornerRadiusPreset.Sm);

        var surfaceColor = _isDarkMode ? Color.Parse("#252B33") : Color.Parse("#F8F8F8");
        var textColor = _isDarkMode ? ThemeColors.DarkText : ThemeColors.LightText;
        var textSecondaryColor = _isDarkMode ? ThemeColors.DarkTextSecondary : ThemeColors.LightTextSecondary;
        var borderColor = _isDarkMode ? Color.Parse("#3D4450") : Color.Parse("#E8E8E8");

        var coverSize = Math.Clamp(basis * 0.085, 36, 52);
        var coverBorder = new Border
        {
            Width = coverSize,
            Height = coverSize,
            CornerRadius = new CornerRadius(cardCornerRadius * 0.6),
            Background = new SolidColorBrush(_isDarkMode ? Color.Parse("#3D4450") : Color.Parse("#E8E8E8")),
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Center
        };

        var fallbackIcon = new SymbolIcon
        {
            Symbol = Symbol.MusicNote1,
            IconVariant = IconVariant.Regular,
            FontSize = coverSize * 0.45,
            Foreground = new SolidColorBrush(_isDarkMode ? ThemeColors.DarkTextSecondary : ThemeColors.LightTextSecondary),
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

        var voteHost = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 3,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            IsVisible = song.VoteCount > 0
        };

        var voteIcon = new SymbolIcon
        {
            Symbol = Symbol.Fire,
            IconVariant = IconVariant.Filled,
            FontSize = detailSize * 1.1,
            Foreground = new SolidColorBrush(ThemeColors.Warning),
            VerticalAlignment = VerticalAlignment.Center
        };

        var voteText = new TextBlock
        {
            Text = song.VoteCount.ToString(),
            FontSize = detailSize,
            FontWeight = FontWeight.Medium,
            Foreground = new SolidColorBrush(ThemeColors.Warning),
            VerticalAlignment = VerticalAlignment.Center
        };

        voteHost.Children.Add(voteIcon);
        voteHost.Children.Add(voteText);

        var contentGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 10
        };

        contentGrid.Children.Add(coverBorder);
        contentGrid.Children.Add(infoPanel);
        contentGrid.Children.Add(voteHost);
        Grid.SetColumn(infoPanel, 1);
        Grid.SetColumn(voteHost, 2);

        return new Border
        {
            Background = new SolidColorBrush(surfaceColor),
            BorderBrush = new SolidColorBrush(borderColor),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(cardCornerRadius),
            Padding = new Thickness(10, 8),
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
        var cornerRadius = _isDesignMode ? 24d : ResolveCurrentCornerRadius(PluginCornerRadiusPreset.Lg);
        var smRadius = _isDesignMode ? 10d : ResolveCurrentCornerRadius(PluginCornerRadiusPreset.Sm);

        RootBorder.CornerRadius = new CornerRadius(cornerRadius);
        CardBackground.CornerRadius = new CornerRadius(cornerRadius);
        CardBorder.CornerRadius = new CornerRadius(cornerRadius);

        var padding = Math.Clamp(basis * 0.035, 10, 18);
        CardBorder.Padding = new Thickness(padding, padding * 0.875, padding, padding * 0.875);

        HeaderGrid.ColumnSpacing = Math.Clamp(basis * 0.025, 8, 14);
        HeaderGrid.Margin = new Thickness(0, 0, 0, Math.Clamp(basis * 0.03, 8, 14));

        var iconBadgeSize = Math.Clamp(basis * 0.08, 32, 42);
        HeaderIconBadge.Width = iconBadgeSize;
        HeaderIconBadge.Height = iconBadgeSize;
        HeaderIconBadge.CornerRadius = new CornerRadius(iconBadgeSize * 0.28);
        HeaderIcon.FontSize = Math.Clamp(basis * 0.04, 14, 20);

        HeaderStack.Spacing = Math.Clamp(basis * 0.005, 1, 4);
        HeaderText.FontSize = Math.Clamp(basis * 0.045, 14, 20);
        DateText.FontSize = Math.Clamp(basis * 0.032, 11, 15);

        var refreshButtonSize = Math.Clamp(basis * 0.08, 30, 40);
        RefreshButton.Width = refreshButtonSize;
        RefreshButton.Height = refreshButtonSize;
        RefreshButton.CornerRadius = new CornerRadius(refreshButtonSize / 2);
        RefreshIcon.FontSize = Math.Clamp(basis * 0.035, 13, 18);

        SongsStackPanel.Spacing = Math.Clamp(basis * 0.02, 5, 10);

        LoadingHost.CornerRadius = new CornerRadius(smRadius);
        ErrorHost.CornerRadius = new CornerRadius(smRadius);
        EmptyHost.CornerRadius = new CornerRadius(smRadius);

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

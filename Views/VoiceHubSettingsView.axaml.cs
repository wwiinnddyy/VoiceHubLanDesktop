using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using FluentAvalonia.UI.Controls;
using VoiceHubLanDesktop.ViewModels;

namespace VoiceHubLanDesktop.Views;

public partial class VoiceHubSettingsView : UserControl
{
    private VoiceHubSettingsViewModel? _viewModel;
    private bool _isDarkMode;

    // Controls
    private Border? _connectionStatusBorder;
    private FontIcon? _connectionStatusIcon;
    private TextBlock? _connectionStatusText;

    private static class ThemeColors
    {
        public static readonly Color Primary = Color.Parse("#D43C33");
        public static readonly Color PrimaryHover = Color.Parse("#E85A52");

        // Light theme
        public static readonly Color LightCardBackground = Color.Parse("#FCFBFA");
        public static readonly Color LightCardBorder = Color.Parse("#E8E8E8");
        public static readonly Color LightTextPrimary = Color.Parse("#2B2F35");
        public static readonly Color LightTextSecondary = Color.Parse("#7A8088");
        public static readonly Color LightInputBackground = Color.Parse("#FFFFFF");
        public static readonly Color LightInputBorder = Color.Parse("#D1D5DB");
        public static readonly Color LightHoverBackground = Color.Parse("#F3F4F6");
        public static readonly Color LightIconBadgeBackground = Color.Parse("#14D43C33");
        public static readonly Color LightIconBadgeBorder = Color.Parse("#20D43C33");

        // Dark theme
        public static readonly Color DarkCardBackground = Color.Parse("#1B2129");
        public static readonly Color DarkCardBorder = Color.Parse("#2D3440");
        public static readonly Color DarkTextPrimary = Color.Parse("#E8EAED");
        public static readonly Color DarkTextSecondary = Color.Parse("#A8B1C2");
        public static readonly Color DarkInputBackground = Color.Parse("#252B33");
        public static readonly Color DarkInputBorder = Color.Parse("#3D4450");
        public static readonly Color DarkHoverBackground = Color.Parse("#2D3440");
        public static readonly Color DarkIconBadgeBackground = Color.Parse("#2D3440");
        public static readonly Color DarkIconBadgeBorder = Color.Parse("#3D4450");

        // Status colors
        public static readonly Color SuccessBackground = Color.Parse("#E8F5E9");
        public static readonly Color SuccessForeground = Color.Parse("#2E7D32");
        public static readonly Color ErrorBackground = Color.Parse("#FFEBEE");
        public static readonly Color ErrorForeground = Color.Parse("#D32F2F");

        // Dark status colors
        public static readonly Color DarkSuccessBackground = Color.Parse("#1B3A1C");
        public static readonly Color DarkErrorBackground = Color.Parse("#3A1C1C");
    }

    public VoiceHubSettingsView()
    {
        InitializeComponent();

        // Get references to named controls
        _connectionStatusBorder = this.FindControl<Border>("ConnectionStatusBorder");
        _connectionStatusIcon = this.FindControl<FontIcon>("ConnectionStatusIcon");
        _connectionStatusText = this.FindControl<TextBlock>("ConnectionStatusText");

        _isDarkMode = ResolveIsDarkMode();
        ApplyTheme();

        DataContextChanged += OnDataContextChanged;
        ActualThemeVariantChanged += OnThemeVariantChanged;
    }

    public override void OnNavigatedTo(object? parameter)
    {
        // Refresh settings when navigated to
        if (DataContext is VoiceHubSettingsViewModel vm)
        {
            _viewModel = vm;
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as VoiceHubSettingsViewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            UpdateConnectionStatus();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(VoiceHubSettingsViewModel.ShowConnectionStatus)
            or nameof(VoiceHubSettingsViewModel.IsConnectionSuccess)
            or nameof(VoiceHubSettingsViewModel.ConnectionStatusText)
            or nameof(VoiceHubSettingsViewModel.ConnectionStatusIcon))
        {
            UpdateConnectionStatus();
        }
    }

    private void UpdateConnectionStatus()
    {
        if (_viewModel is null || _connectionStatusBorder is null ||
            _connectionStatusIcon is null || _connectionStatusText is null)
        {
            return;
        }

        if (!_viewModel.ShowConnectionStatus)
        {
            _connectionStatusBorder.IsVisible = false;
            return;
        }

        _connectionStatusBorder.IsVisible = true;
        _connectionStatusText.Text = _viewModel.ConnectionStatusText;
        _connectionStatusIcon.Glyph = _viewModel.ConnectionStatusIcon switch
        {
            FluentIcons.Common.Symbol.CheckmarkCircle => "",
            FluentIcons.Common.Symbol.DismissCircle => "",
            _ => ""
        };

        if (_viewModel.IsConnectionSuccess)
        {
            _connectionStatusBorder.Background = new SolidColorBrush(
                _isDarkMode ? ThemeColors.DarkSuccessBackground : ThemeColors.SuccessBackground);
            _connectionStatusIcon.Foreground = new SolidColorBrush(ThemeColors.SuccessForeground);
            _connectionStatusText.Foreground = new SolidColorBrush(ThemeColors.SuccessForeground);
        }
        else
        {
            _connectionStatusBorder.Background = new SolidColorBrush(
                _isDarkMode ? ThemeColors.DarkErrorBackground : ThemeColors.ErrorBackground);
            _connectionStatusIcon.Foreground = new SolidColorBrush(ThemeColors.ErrorForeground);
            _connectionStatusText.Foreground = new SolidColorBrush(ThemeColors.ErrorForeground);
        }
    }

    private void OnThemeVariantChanged(object? sender, EventArgs e)
    {
        var newIsDarkMode = ResolveIsDarkMode();
        if (_isDarkMode != newIsDarkMode)
        {
            _isDarkMode = newIsDarkMode;
            ApplyTheme();
            UpdateConnectionStatus();
        }
    }

    private bool ResolveIsDarkMode()
    {
        if (ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark)
        {
            return true;
        }

        if (ActualThemeVariant == Avalonia.Styling.ThemeVariant.Light)
        {
            return false;
        }

        return Application.Current?.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
    }

    private void ApplyTheme()
    {
        if (_isDarkMode)
        {
            ApplyDarkTheme();
        }
        else
        {
            ApplyLightTheme();
        }
    }

    private void ApplyLightTheme()
    {
        // Use shared Fluent Avalonia theme resources
        Resources["CardBackgroundBrush"] = new SolidColorBrush(ThemeColors.LightCardBackground);
        Resources["CardBorderBrush"] = new SolidColorBrush(ThemeColors.LightCardBorder);
        Resources["SuccessBackgroundBrush"] = new SolidColorBrush(ThemeColors.SuccessBackground);
        Resources["SuccessForegroundBrush"] = new SolidColorBrush(ThemeColors.SuccessForeground);
        Resources["ErrorBackgroundBrush"] = new SolidColorBrush(ThemeColors.ErrorBackground);
        Resources["ErrorForegroundBrush"] = new SolidColorBrush(ThemeColors.ErrorForeground);
    }

    private void ApplyDarkTheme()
    {
        // Use shared Fluent Avalonia theme resources
        Resources["CardBackgroundBrush"] = new SolidColorBrush(ThemeColors.DarkCardBackground);
        Resources["CardBorderBrush"] = new SolidColorBrush(ThemeColors.DarkCardBorder);
        Resources["SuccessBackgroundBrush"] = new SolidColorBrush(ThemeColors.DarkSuccessBackground);
        Resources["SuccessForegroundBrush"] = new SolidColorBrush(ThemeColors.SuccessForeground);
        Resources["ErrorBackgroundBrush"] = new SolidColorBrush(ThemeColors.DarkErrorBackground);
        Resources["ErrorForegroundBrush"] = new SolidColorBrush(ThemeColors.ErrorForeground);
    }

    private async void OnTestConnectionClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel is null) return;

        if (sender is Button button)
        {
            button.IsEnabled = false;
        }

        try
        {
            await _viewModel.TestConnectionAsync();
        }
        finally
        {
            if (sender is Button btn)
            {
                btn.IsEnabled = true;
            }
        }
    }

    private void OnSaveClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewModel?.SaveSettings();
    }

    private void OnResetClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewModel?.ResetToOriginal();
    }
}

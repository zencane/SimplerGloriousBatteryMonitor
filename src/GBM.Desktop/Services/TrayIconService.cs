using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using GBM.Core.Models;
using GBM.Core.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace GBM.Desktop.Services;

public class TrayIconService : IDisposable
{
    private static readonly int[] RefreshRateOptionsSeconds = { 5, 60, 300, 600 };

    private readonly IBatteryMonitorService _monitorService;
    private readonly ISettingsService _settingsService;
    private readonly IAutoStartService _autoStartService;
    private readonly ILogger<TrayIconService> _logger;
    private TrayIcon? _trayIcon;
    private NativeMenu? _menu;
    private NativeMenuItem? _infoItem;
    private NativeMenuItem? _startWithWindowsItem;
    private readonly List<NativeMenuItem> _refreshRateItems = new();
    private WindowIcon? _currentIcon;
    private int _lastLevel;
    private bool _lastCharging;
    private bool _lastConnected;
    private string _lastTooltipText = string.Empty;
    private string _lastInfoHeaderText = string.Empty;

    public TrayIconService(
        IBatteryMonitorService monitorService,
        ISettingsService settingsService,
        IAutoStartService autoStartService,
        ILogger<TrayIconService> logger)
    {
        _monitorService = monitorService;
        _settingsService = settingsService;
        _autoStartService = autoStartService;
        _logger = logger;
    }

    public void Initialize()
    {
        try
        {
            _menu = new NativeMenu();

            _infoItem = new NativeMenuItem("Mouse Not Found") { IsEnabled = false };
            _menu.Items.Add(_infoItem);
            _menu.Items.Add(new NativeMenuItemSeparator());

            var rescanItem = new NativeMenuItem("Rescan");
            rescanItem.Click += (_, _) => _monitorService.TriggerRescan();
            _menu.Items.Add(rescanItem);

            _menu.Items.Add(new NativeMenuItemSeparator());

            _startWithWindowsItem = new NativeMenuItem("Start with Windows")
            {
                ToggleType = MenuItemToggleType.CheckBox,
                IsChecked = _autoStartService.IsEnabled()
            };
            _startWithWindowsItem.Click += (_, _) => ToggleStartWithWindows();
            _menu.Items.Add(_startWithWindowsItem);

            var refreshRateMenu = new NativeMenu();
            int currentInterval = NormalizeRefreshInterval(_settingsService.Current.RefreshIntervalSeconds);
            foreach (var seconds in RefreshRateOptionsSeconds)
            {
                var item = new NativeMenuItem(FormatRefreshRateLabel(seconds))
                {
                    ToggleType = NativeMenuItemToggleType.Radio,
                    IsChecked = seconds == currentInterval
                };
                item.Click += (_, _) => SetRefreshInterval(seconds);
                _refreshRateItems.Add(item);
                refreshRateMenu.Items.Add(item);
            }

            var refreshRateParent = new NativeMenuItem("Refresh Rate") { Menu = refreshRateMenu };
            _menu.Items.Add(refreshRateParent);

            _menu.Items.Add(new NativeMenuItemSeparator());

            var quitItem = new NativeMenuItem("Quit");
            quitItem.Click += (_, _) =>
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    desktop.Shutdown();
            };
            _menu.Items.Add(quitItem);

            _trayIcon = new TrayIcon
            {
                ToolTipText = "Glorious Battery Monitor",
                Menu = _menu,
                IsVisible = true
            };
            _lastTooltipText = _trayIcon.ToolTipText ?? string.Empty;

            try
            {
                var uri = new Uri("avares://GBM.Desktop/Assets/app-icon.ico");
                _currentIcon = new WindowIcon(AssetLoader.Open(uri));
                _trayIcon.Icon = _currentIcon;
            }
            catch
            {
                // Icon not critical
            }

            _monitorService.BatteryStateChanged += OnBatteryStateChanged;
            _lastInfoHeaderText = _infoItem.Header?.ToString() ?? string.Empty;
            _logger.LogInformation("[TRAY] Tray icon initialized");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TRAY] Failed to initialize tray icon");
        }
    }

    private void ToggleStartWithWindows()
    {
        if (_startWithWindowsItem == null)
            return;

        bool newValue = !_autoStartService.IsEnabled();
        _autoStartService.SetAutoStart(newValue);
        _startWithWindowsItem.IsChecked = newValue;

        var settings = _settingsService.Current.Clone();
        settings.StartWithOS = newValue;
        _settingsService.Save(settings);
    }

    private void SetRefreshInterval(int seconds)
    {
        foreach (var item in _refreshRateItems)
        {
            item.IsChecked = string.Equals(item.Header?.ToString(), FormatRefreshRateLabel(seconds), StringComparison.Ordinal);
        }

        var settings = _settingsService.Current.Clone();
        settings.RefreshIntervalSeconds = seconds;
        _settingsService.Save(settings);
    }

    private static int NormalizeRefreshInterval(int seconds)
    {
        foreach (var option in RefreshRateOptionsSeconds)
        {
            if (option == seconds)
                return option;
        }

        return RefreshRateOptionsSeconds[0];
    }

    private static string FormatRefreshRateLabel(int seconds) => seconds switch
    {
        5 => "5 seconds",
        60 => "1 minute",
        300 => "5 minutes",
        600 => "10 minutes",
        _ => $"{seconds} seconds"
    };

    /// <summary>
    /// Rounds up to the nearest multiple of 5 (e.g. 71% displays as "75").
    /// </summary>
    private static int RoundUpToNearest5(int level)
    {
        level = Math.Clamp(level, 0, 100);
        return ((level + 4) / 5) * 5;
    }

    private void OnBatteryStateChanged(BatteryState state)
    {
        try
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var newLevel = RoundUpToNearest5(state.Level);
                var newCharging = state.IsCharging;
                var newConnected = state.Connection == ConnectionState.Connected;

                bool iconNeedsUpdate = newLevel != _lastLevel
                                    || newCharging != _lastCharging
                                    || newConnected != _lastConnected;

                _lastLevel = newLevel;
                _lastCharging = newCharging;
                _lastConnected = newConnected;

                if (iconNeedsUpdate)
                    UpdateTrayIcon();

                var toolTipText = !_lastConnected
                    ? state.Connection == ConnectionState.LastKnown
                        ? $"Last known: {newLevel}%"
                        : "Mouse Not Found"
                    : $"{state.DeviceName} — {newLevel}%{(state.IsCharging ? " (Charging)" : string.Empty)}";
                UpdateToolTipText(toolTipText);

                var infoHeader = _lastConnected
                    ? $"{state.DeviceName} — {newLevel}%"
                    : "Mouse Not Found";
                UpdateInfoHeader(infoHeader);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TRAY] Error updating tray");
        }
    }

    private void UpdateTrayIcon()
    {
        if (_trayIcon == null) return;

        var icon = TrayIconRenderer.RenderIcon(_lastLevel, _lastCharging, _lastConnected);

        if (icon != null && !ReferenceEquals(_currentIcon, icon))
        {
            _currentIcon = icon;
            _trayIcon.Icon = icon;
        }
    }

    private void UpdateToolTipText(string text)
    {
        if (_trayIcon == null || string.Equals(_lastTooltipText, text, StringComparison.Ordinal))
            return;

        _trayIcon.ToolTipText = text;
        _lastTooltipText = text;
    }

    private void UpdateInfoHeader(string text)
    {
        if (_infoItem == null || string.Equals(_lastInfoHeaderText, text, StringComparison.Ordinal))
            return;

        _infoItem.Header = text;
        _lastInfoHeaderText = text;
    }

    public void Dispose()
    {
        _monitorService.BatteryStateChanged -= OnBatteryStateChanged;
        if (_trayIcon != null)
        {
            _trayIcon.IsVisible = false;
            _trayIcon.Dispose();
            _currentIcon = null;
        }
    }
}

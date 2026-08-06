using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GBM.Core.Services;
using GBM.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace GBM.Desktop;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private TrayIconService? _trayService;

    public static ServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
            Services = _serviceProvider;

            // Tray-only app: never show a window.
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;

            _trayService = _serviceProvider.GetRequiredService<TrayIconService>();
            _trayService.Initialize();

            var monitorService = _serviceProvider.GetRequiredService<IBatteryMonitorService>();
            _ = monitorService.StartAsync();

            desktop.ShutdownRequested += OnShutdown;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        var settingsPath = GetSettingsPath();
        var logPath = System.IO.Path.Combine(settingsPath, "debug.log");
        System.IO.Directory.CreateDirectory(settingsPath);

        // Rename previous session log before overwriting, so crash logs survive
        string prevLogPath = System.IO.Path.Combine(settingsPath, "debug.prev.log");
        try
        {
            if (System.IO.File.Exists(logPath))
            {
                if (System.IO.File.Exists(prevLogPath))
                    System.IO.File.Delete(prevLogPath);
                System.IO.File.Move(logPath, prevLogPath);
            }
        }
        catch { }

        bool debugMode = System.Environment.GetEnvironmentVariable("GBM_DEBUG") == "1";
        var minSerilogLevel = debugMode
            ? Serilog.Events.LogEventLevel.Debug
            : Serilog.Events.LogEventLevel.Information;

        var serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Is(minSerilogLevel)
            .WriteTo.File(logPath,
                rollingInterval: RollingInterval.Infinite,
                fileSizeLimitBytes: 5 * 1024 * 1024,
                rollOnFileSizeLimit: true)
            .CreateLogger();

        services.AddLogging(builder =>
        {
            builder.AddSerilog(serilogLogger, dispose: true);
            builder.SetMinimumLevel(debugMode ? LogLevel.Debug : LogLevel.Information);
        });

        // Core services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IStorageService, StorageService>();
        services.AddSingleton<IHidDeviceService, HidDeviceService>();
        services.AddSingleton<IBatteryMonitorService, BatteryMonitorService>();
        services.AddSingleton<IAutoStartService, AutoStartService>();

        // Desktop services
        services.AddSingleton<TrayIconService>();
    }

    private void OnShutdown(object? sender, ShutdownRequestedEventArgs e)
    {
        _trayService?.Dispose();

        if (_serviceProvider?.GetService<IBatteryMonitorService>() is BatteryMonitorService monitor)
        {
            _ = monitor.StopAsync();
        }
        _serviceProvider?.Dispose();
    }

    private static string GetSettingsPath()
    {
        return System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
            "GloriousBatteryMonitor");
    }
}

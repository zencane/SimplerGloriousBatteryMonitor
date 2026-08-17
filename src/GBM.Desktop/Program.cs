using Avalonia;
using System.Threading;

namespace GBM.Desktop;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Prevent multiple instances — exit silently if already running
        using var mutex = new Mutex(true, "GloriousBatteryMonitor_SingleInstance", out bool isNew);
        if (!isNew)
        {
            for (int i = 0; i < 50 && !isNew; i++)
            {
                Thread.Sleep(100);
                try
                {
                    isNew = mutex.WaitOne(0);
                }
                catch (AbandonedMutexException)
                {
                    isNew = true;
                }
            }

            if (!isNew)
                return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect();
}

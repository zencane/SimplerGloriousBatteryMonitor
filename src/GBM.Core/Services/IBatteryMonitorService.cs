using GBM.Core.Models;

namespace GBM.Core.Services;

public interface IBatteryMonitorService
{
    BatteryState CurrentState { get; }
    bool IsRunning { get; }
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
    void TriggerRescan();
    event Action<BatteryState>? BatteryStateChanged;
    /// <summary>
    /// Fired during device discovery to report human-readable probe status.
    /// </summary>
    event Action<string>? ProbeStatusChanged;
}

namespace GBM.Core.Models;

public enum ConnectionState
{
    NotConnected,
    Connecting,
    Connected,
    LastKnown,
    Sleeping
}

public record BatteryState
{
    public int Level { get; init; }
    public bool IsCharging { get; init; }
    public ConnectionState Connection { get; init; }
    public string DeviceName { get; init; } = "Glorious Mouse";
    public DateTime LastReadTime { get; init; }

    public static BatteryState Disconnected => new()
    {
        Level = 0,
        IsCharging = false,
        Connection = ConnectionState.NotConnected,
        DeviceName = "Glorious Mouse",
        LastReadTime = DateTime.MinValue
    };
}

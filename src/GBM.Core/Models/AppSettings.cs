using System.Text.Json.Serialization;

namespace GBM.Core.Models;

public class AppSettings
{
    public bool StartWithOS { get; set; } = false;
    public int RefreshIntervalSeconds { get; set; } = 5;

    public AppSettings Clone() => new()
    {
        StartWithOS = StartWithOS,
        RefreshIntervalSeconds = RefreshIntervalSeconds
    };
}

[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(List<DeviceProfile>))]
public partial class GbmJsonContext : JsonSerializerContext
{
}

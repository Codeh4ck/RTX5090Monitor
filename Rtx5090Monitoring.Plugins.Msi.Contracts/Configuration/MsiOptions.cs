using System.Text.Json.Serialization;

namespace Rtx5090Monitoring.Plugins.Msi.Contracts.Configuration;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MsiOptions
{
    public string? DevicePath { get; init; }
    public MsiFanControl FanControl { get; init; } = MsiFanControl.Unmanaged;
    public int FanDutyPercent { get; init; } = 50;

    public void Validate()
    {
        if (!Enum.IsDefined(FanControl) || FanDutyPercent is < 0 or > 100
            || DevicePath is { Length: > 512 } || DevicePath is not null && string.IsNullOrWhiteSpace(DevicePath))
        {
            throw new ArgumentException("MSI fan mode must be valid, duty must be 0–100%, and device path must be empty or a valid device identifier.");
        }
    }
}

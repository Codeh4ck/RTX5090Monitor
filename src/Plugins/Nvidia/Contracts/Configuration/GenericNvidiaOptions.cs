using System.Text.Json.Serialization;

namespace Rtx5090Monitoring.Plugins.GenericNvidia.Contracts.Configuration;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GenericNvidiaOptions
{
    public string? DeviceId { get; init; }
    public double? CoreTemperatureWarningC { get; init; } = 81;
    public double? MemoryTemperatureWarningC { get; init; } = 85;
    public string ElectricalSource { get; init; } = "Automatic";

    public void Validate()
    {
        if (DeviceId is not null && string.IsNullOrWhiteSpace(DeviceId))
        {
            throw new ArgumentException("GPU selection must be null or a nonempty device identifier.");
        }

        foreach (var threshold in new[] { CoreTemperatureWarningC, MemoryTemperatureWarningC })
        {
            if (threshold is { } value && (!double.IsFinite(value) || value is < 30 or > 120))
            {
                throw new ArgumentException("GPU temperature warning thresholds must be between 30 and 120 °C.");
            }
        }

        if (ElectricalSource is not ("Automatic" or "WireView" or "None"))
        {
            throw new ArgumentException("GPU electrical source must be Automatic, WireView or None.");
        }
    }
}

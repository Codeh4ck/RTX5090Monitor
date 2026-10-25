using System.Text.Json.Serialization;

namespace Rtx5090Monitoring.Plugins.Asus.Contracts.Configuration;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AsusOptions
{
    public double? CoreTemperatureWarningC { get; init; }
    public double? MemoryTemperatureWarningC { get; init; }
    [JsonIgnore] public double EffectiveCoreTemperatureWarningC => CoreTemperatureWarningC ?? 84;
    [JsonIgnore] public double EffectiveMemoryTemperatureWarningC => MemoryTemperatureWarningC ?? 95;
    public string? DeviceId { get; init; }
    public int ImbalanceAmperageThreshold { get; init; } = 6;
    public int ImbalancePercentageThreshold { get; init; } = 30;
    public int ImbalanceDurationThresholdMs { get; init; } = 3000;
    public float WireOvercurrentAmperageThreshold { get; init; } = 9.2f;
    public int WireOvercurrentThresholdMs { get; init; } = 3000;

    public void Validate()
    {
        if (!double.IsFinite(EffectiveCoreTemperatureWarningC) || EffectiveCoreTemperatureWarningC is < 30 or > 120
            || !double.IsFinite(EffectiveMemoryTemperatureWarningC) || EffectiveMemoryTemperatureWarningC is < 30 or > 120)
        {
            throw new ArgumentException("ASUS temperature warning thresholds must be between 30 and 120 °C.");
        }

        if (DeviceId is not null && string.IsNullOrWhiteSpace(DeviceId))
        {
            throw new ArgumentException("ASUS device ID must be null or a nonempty hardware identifier.");
        }

        if (ImbalanceAmperageThreshold is < 1 or > 10 || ImbalancePercentageThreshold is < 10 or > 50
            || ImbalanceDurationThresholdMs is < 100 or > 30000
            || !float.IsFinite(WireOvercurrentAmperageThreshold) || WireOvercurrentAmperageThreshold is < 6 or > 10.5f
            || WireOvercurrentThresholdMs is < 100 or > 30000)
        {
            throw new ArgumentException("Invalid ASUS imbalance or overcurrent thresholds.");
        }
    }
}

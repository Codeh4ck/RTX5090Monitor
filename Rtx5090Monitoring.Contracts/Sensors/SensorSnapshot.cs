using Rtx5090Monitoring.Contracts.Health;

using Rtx5090Monitoring.Contracts.Alerts;

namespace Rtx5090Monitoring.Contracts.Sensors;

public sealed record SensorSnapshot(int SchemaVersion, long Sequence, DateTimeOffset Timestamp, SensorReading[] Sensors,
    PluginHealth[] Plugins, ResolvedSelection[] Selections, long ConfigurationRevision = 0)
{
    public Warnings.WarningSnapshot? Warnings { get; init; }
    public SensorAlert[] Alerts { get; init; } = [];
}

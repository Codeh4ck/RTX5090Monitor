using RTXLens.Contracts.Health;

using RTXLens.Contracts.Alerts;

namespace RTXLens.Contracts.Sensors;

public sealed record SensorSnapshot(int SchemaVersion, long Sequence, DateTimeOffset Timestamp, SensorReading[] Sensors,
    PluginHealth[] Plugins, ResolvedSelection[] Selections, long ConfigurationRevision = 0)
{
    public Configuration.ActiveSettingsProfile? SettingsProfile { get; init; }
    public Warnings.PinWarningLimit[]? PinWarningLimits { get; init; }
    public Warnings.WarningSnapshot? Warnings { get; init; }
    public SensorAlert[] Alerts { get; init; } = [];
    public SafeguardExecutionNotice[]? SafeguardExecutions { get; init; }
}

namespace Rtx5090Monitoring.Contracts.Discovery;

public sealed record PluginDefinition(string Id, string Name, PluginCapability[] Capabilities)
{
    public Enums.SensorMetric[] ConnectorMetrics { get; init; } = [];
    public Warnings.WarningDefinition[] Warnings { get; init; } = [];
}

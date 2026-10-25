using Rtx5090Monitoring.Contracts.Monitoring;

namespace Rtx5090Monitoring.Contracts.Configuration;

public sealed record ServiceConfiguration
{
    public Warnings.WarningOptions Warnings { get; init; } = new();
    public MonitoringOptions Monitoring { get; init; } = new();
    public Dictionary<string, PluginSettings> Plugins { get; init; } = new(StringComparer.Ordinal);
}

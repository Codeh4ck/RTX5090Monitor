using Rtx5090Monitoring.Contracts.Enums;

namespace Rtx5090Monitoring.Contracts.Monitoring;

public sealed class ConnectorMetricSelection
{
    public SensorMetric Metric { get; set; }
    public bool AllowFallback { get; set; } = true;
    public List<string> PreferredPlugins { get; set; } = [];

    public bool IsValid() => Metric is SensorMetric.Power or SensorMetric.Current or SensorMetric.Voltage
        && PreferredPlugins is not null && PreferredPlugins.All(id => !string.IsNullOrWhiteSpace(id))
        && PreferredPlugins.Distinct(StringComparer.Ordinal).Count() == PreferredPlugins.Count;
}

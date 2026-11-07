namespace Rtx5090Monitoring.Contracts.Monitoring;

public sealed class ConnectorSourceProfile
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<ConnectorMetricSelection> Metrics { get; set; } = [];
    public Dictionary<string, string> Connectors { get; set; } = new(StringComparer.Ordinal);

    public bool IsValid() => !string.IsNullOrWhiteSpace(Id) && !string.IsNullOrWhiteSpace(Name)
        && Metrics is { Count: 3 } && Metrics.All(metric => metric is not null && metric.IsValid())
        && Metrics.Select(metric => metric.Metric).Distinct().Count() == 3
        && Connectors is not null && Connectors.All(pair => !string.IsNullOrWhiteSpace(pair.Key)
            && !string.IsNullOrWhiteSpace(pair.Value));
}

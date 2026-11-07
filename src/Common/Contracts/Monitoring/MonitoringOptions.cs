namespace Rtx5090Monitoring.Contracts.Monitoring;

public sealed class MonitoringOptions
{
    public int StreamIntervalMs { get; set; } = 1000;
    public ConnectorProfiles ConnectorProfiles { get; set; } = new();
    public List<SensorSelection> Selections { get; set; } = [];

    public bool IsValid() => StreamIntervalMs is >= 100 and <= 30000 && Selections is not null
                             && ConnectorProfiles is not null && ConnectorProfiles.IsValid()
                             && Selections.All(x => x is not null && x.IsValid())
                             && Selections.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() == Selections.Count;
}

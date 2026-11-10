using RTXLens.Contracts.Sensors;

namespace RTXLens.Contracts.Monitoring;

public sealed class SensorSelection
{
    public string Name { get; set; } = string.Empty;
    public bool AllowFallback { get; set; }
    public bool AllowDifferentScope { get; set; }
    public List<SensorIdentity> PreferredSources { get; set; } = [];

    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(Name)
        && PreferredSources is { Count: > 0 }
        && PreferredSources.All(x => SensorValidation.IsValidIdentity(x) && x.Metric == PreferredSources[0].Metric
                                                                         && (AllowDifferentScope || (x.Component == PreferredSources[0].Component &&
                                                                             x.Channel == PreferredSources[0].Channel &&
                                                                             x.ConnectorId == PreferredSources[0].ConnectorId)))
        && PreferredSources.Select(x => x.Key).Distinct(StringComparer.Ordinal).Count() == PreferredSources.Count;
}

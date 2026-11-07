namespace Rtx5090Monitoring.Contracts.Monitoring;

public sealed class ConnectorProfiles
{
    public int Version { get; set; }
    public string ActiveProfileId { get; set; } = "automatic";
    public List<ConnectorSourceProfile> Profiles { get; set; } = [];

    public static string SelectionName(Enums.SensorMetric metric) => "gpu-status/" + metric.ToString().ToLowerInvariant();

    public bool IsValid() => Profiles is not null && !string.IsNullOrWhiteSpace(ActiveProfileId)
        && (Version == 0 && Profiles.Count == 0 || Version == 1 && Profiles is { Count: > 0 }
        && Profiles.All(profile => profile is not null && profile.IsValid())
        && Profiles.Select(profile => profile.Id).Distinct(StringComparer.Ordinal).Count() == Profiles.Count
        && Profiles.Any(profile => profile.Id == ActiveProfileId));
}

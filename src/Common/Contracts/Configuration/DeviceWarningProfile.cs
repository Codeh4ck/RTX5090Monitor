using Rtx5090Monitoring.Contracts.Warnings;

namespace Rtx5090Monitoring.Contracts.Configuration;

public sealed record DeviceWarningProfile
{
    public const string DefaultId = "automatic-astral";

    public string Id { get; init; } = DefaultId;
    public string Name { get; init; } = "Astral";
    public string PluginId { get; init; } = "asus-astral";
    public string? DeviceId { get; init; }
    public string? ConnectorId { get; init; }
    public bool Enabled { get; init; } = true;
    public ConnectorWarningLimits? Limits { get; init; }
    public WarningPolicy? NotificationPolicy { get; init; }
    public int? ShutdownAfterSeconds { get; init; }

    public bool IsValid() => (Id == DefaultId || Guid.TryParseExact(Id, "N", out _))
        && !string.IsNullOrWhiteSpace(Name) && Name.Length <= 80
        && (DeviceId is null || !string.IsNullOrWhiteSpace(DeviceId) && DeviceId.Length <= 512)
        && (PluginId == "msi-psu" ? ConnectorId is "1" or "2" : PluginId is "asus-astral" or "wireview-pro2" && ConnectorId is null)
        && (Limits is null || Limits.IsValid())
        && (NotificationPolicy is null || NotificationPolicy.IsValid())
        && ShutdownAfterSeconds is null or >= 1 and <= 86399;
}

public sealed record ConnectorWarningLimits(double OvercurrentAmperes, int OvercurrentMilliseconds,
    int ImbalanceActivationAmperes, int ImbalancePercentage, int ImbalanceMilliseconds,
    double LowPinVoltage, double HighPinVoltage)
{
    public static ConnectorWarningLimits From(SettingsProfile profile) => new(profile.OvercurrentAmperes,
        profile.OvercurrentMilliseconds, profile.ImbalanceActivationAmperes, profile.ImbalancePercentage,
        profile.ImbalanceMilliseconds, profile.LowPinVoltage, profile.HighPinVoltage);

    public bool IsValid() => OvercurrentAmperes is >= 6 and <= 10.5
        && OvercurrentMilliseconds is >= 100 and <= 30000
        && ImbalanceActivationAmperes is >= 1 and <= 10 && ImbalancePercentage is >= 10 and <= 50
        && ImbalanceMilliseconds is >= 100 and <= 30000
        && LowPinVoltage is >= 5 and < 12 && HighPinVoltage is > 12 and <= 20;
}

public sealed record WarningProfileTarget(string PluginId, string DeviceId, string Name);
public sealed record DeviceWarningProfileCatalog(DeviceWarningProfile[] Profiles, WarningProfileTarget[] Targets,
    ConnectorWarningLimits Defaults, ConnectorWarningLimits AstralDefaults, string GpuModel, WarningPolicy DefaultPolicy);

public sealed record WarningShutdownReason(string Id, DateTimeOffset RequestedAt, DateTimeOffset BootTime,
    string Source, string ProfileName, string Warning, string Detail);

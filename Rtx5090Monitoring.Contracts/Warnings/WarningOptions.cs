using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rtx5090Monitoring.Contracts.Warnings;

public sealed record WarningOptions
{
    public bool NotificationsEnabled { get; init; } = true;
    public WarningPolicy Defaults { get; init; } = new();
    public Dictionary<string, WarningPolicy> Overrides { get; init; } = new(StringComparer.Ordinal);
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalSettings { get; set; }

    public WarningPolicy PolicyFor(string typeId) => Overrides.GetValueOrDefault(typeId) ?? Defaults;
    public bool IsValid() => Defaults is not null && Defaults.IsValid() && Overrides is not null
        && Overrides.All(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Key.Length <= 200 && pair.Value is not null && pair.Value.IsValid());
}

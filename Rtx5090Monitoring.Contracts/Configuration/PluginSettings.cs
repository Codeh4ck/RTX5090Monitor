using System.Text.Json;

namespace Rtx5090Monitoring.Contracts.Configuration;

public sealed record PluginSettings
{
    public bool Enabled { get; init; } = true;
    public bool AutomaticallyDisabled { get; init; }
    public int PollIntervalMs { get; init; } = 1000;
    public int StaleAfterMs { get; init; } = 5000;
    public JsonElement? Options { get; init; }

    public bool IsValid() => PollIntervalMs is >= 100 and <= 30000 && StaleAfterMs >= PollIntervalMs && StaleAfterMs <= 300000;

    public bool HasSameValues(PluginSettings other) => Enabled == other.Enabled && AutomaticallyDisabled == other.AutomaticallyDisabled
        && PollIntervalMs == other.PollIntervalMs && StaleAfterMs == other.StaleAfterMs
        && (Options is null && other.Options is null
            || Options is { } options && other.Options is { } otherOptions && JsonElement.DeepEquals(options, otherOptions));
}

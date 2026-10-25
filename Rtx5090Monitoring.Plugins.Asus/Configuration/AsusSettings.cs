using Rtx5090Monitoring.Plugins.Asus.Contracts.Configuration;
using System.Text.Json;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.Asus.Serialization;

namespace Rtx5090Monitoring.Plugins.Asus.Configuration;

public static class AsusSettings
{
    public static AsusOptions GetOptions(PluginSettings settings)
    {
        var options = settings.Options is { } json
            ? json.Deserialize(AsusJsonContext.Default.AsusOptions) ?? throw new ArgumentException("ASUS options cannot be null.")
            : new AsusOptions();

        options.Validate();
        var maximumDuration = Math.Max(settings.PollIntervalMs, 10000);
        if (options.ImbalanceDurationThresholdMs < settings.PollIntervalMs || options.ImbalanceDurationThresholdMs > maximumDuration
            || options.WireOvercurrentThresholdMs < settings.PollIntervalMs || options.WireOvercurrentThresholdMs > maximumDuration)
        {
            throw new ArgumentException("ASUS detection durations must be between the polling interval and max(polling interval, 10000 ms).");
        }

        return options;
    }

    public static PluginSettings WithOptions(PluginSettings settings, AsusOptions options)
    {
        options.Validate();
        return settings with { Options = JsonSerializer.SerializeToElement(options, AsusJsonContext.Default.AsusOptions) };
    }
}

using RTXLens.Contracts.Configuration;
using RTXLens.Plugins.Asus.Configuration;
using RTXLens.Plugins.Asus.Contracts.Input;
using RTXLens.Plugins.Asus.Contracts.Output;

namespace RTXLens.Plugins.Asus.Mapping;

internal static class AsusConfigMapper
{
    public static ConfigResponse ToResponse(PluginSettings settings)
    {
        var options = AsusSettings.GetOptions(settings);
        return new ConfigResponse
        {
            PollIntervalMs = settings.PollIntervalMs,
            ImbalanceAmperageThreshold = options.ImbalanceAmperageThreshold,
            ImbalancePercentageThreshold = options.ImbalancePercentageThreshold,
            ImbalanceDurationThresholdMs = options.ImbalanceDurationThresholdMs,
            WireOvercurrentAmperageThreshold = options.WireOvercurrentAmperageThreshold,
            WireOvercurrentThresholdMs = options.WireOvercurrentThresholdMs
        };
    }

    public static PluginSettings Apply(ConfigRequest request, PluginSettings previous)
    {
        var options = AsusSettings.GetOptions(previous) with
        {
            ImbalanceAmperageThreshold = request.ImbalanceAmperageThreshold,
            ImbalancePercentageThreshold = request.ImbalancePercentageThreshold,
            ImbalanceDurationThresholdMs = request.ImbalanceDurationThresholdMs,
            WireOvercurrentAmperageThreshold = request.WireOvercurrentAmperageThreshold,
            WireOvercurrentThresholdMs = request.WireOvercurrentThresholdMs
        };

        return AsusSettings.WithOptions(previous with
        {
            PollIntervalMs = request.PollIntervalMs,
            StaleAfterMs = Math.Max(previous.StaleAfterMs, request.PollIntervalMs)
        }, options);
    }
}

using RTXLens.Contracts.Configuration;
using RTXLens.Plugins.Asus.Contracts.Configuration;

namespace RTXLens.Plugins.Asus.Analysis.Monitoring;

public static class AsusProfileOptions
{
    public static AsusOptions Apply(SettingsProfile profile, AsusOptions options) => options with
    {
        WireOvercurrentAmperageThreshold = (float)profile.OvercurrentAmperes,
        WireOvercurrentThresholdMs = profile.OvercurrentMilliseconds,
        ImbalanceAmperageThreshold = profile.ImbalanceActivationAmperes,
        ImbalancePercentageThreshold = profile.ImbalancePercentage,
        ImbalanceDurationThresholdMs = profile.ImbalanceMilliseconds
    };
}

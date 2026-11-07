using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.Asus.Contracts.Configuration;

namespace Rtx5090Monitoring.Plugins.Asus.Analysis.Monitoring;

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

using RTXLens.Plugins.WireView.Contracts.Common;

namespace RTXLens.Debugging.Catalog;

public static class DebugDeviceDefaults
{
    public static DeviceConfig WireView() => new()
    {
        Version = 2,
        FriendlyName = "DEBUG WireView",
        WireOcpFaultThresholdA = 9.2,
        OcpFaultThresholdA = 55,
        OppFaultThresholdW = 600,
        TsFaultThresholdC = 80,
        FanMode = "Automatic",
        FanTempSource = "Onboard",
        FanDutyMax = 100
    };
}
using System.Globalization;
using Rtx5090Monitoring.Plugins.WireView.Contracts.Common;

namespace Rtx5090Monitoring.Presentation.Telemetry;

internal static class WireViewDeviceMetrics
{
    public static MetricViewModel[] Create(DeviceConfig config) =>
    [
        new("Version", Number(config.Version), "pin"),
        new("Friendly name", Text(config.FriendlyName), "pin"),
        new("Backlight duty", Number(config.BacklightDuty, "%"), "voltage"),
        new("Logging interval", Number(config.LoggingInterval), "vcore"),
        new("Averaging", Text(config.Averaging), "vcore"),
        new("Fan mode", Text(config.FanMode), "vfan"),
        new("Fan temperature source", Text(config.FanTempSource), "vtemp"),
        new("Fan duty min / max", Pair(config.FanDutyMin, config.FanDutyMax, "%"), "vfan"),
        new("Fan temperature min / max", Pair(config.FanTempMinC, config.FanTempMaxC, "°C"), "vtemp"),
        new("Overtemperature threshold", Number(config.TsFaultThresholdC, "°C"), "vtemp"),
        new("Total overcurrent threshold", Number(config.OcpFaultThresholdA, "A"), "overcurrent"),
        new("Per-pin overcurrent threshold", Number(config.WireOcpFaultThresholdA, "A"), "overcurrent"),
        new("Overpower threshold", Number(config.OppFaultThresholdW, "W"), "voltage"),
        new("Current imbalance threshold", Number(config.CurrentImbalanceThreshold), "overcurrent"),
        new("Current imbalance minimum load", Number(config.CurrentImbalanceMinLoad), "overcurrent"),
        new("Shutdown wait time", Number(config.ShutdownWaitTimeS, "s"), "vcore"),
        new("Fault display", Flags(config.FaultDisplayEnable), "overcurrent"),
        new("Fault buzzer", Flags(config.FaultBuzzerEnable), "overcurrent"),
        new("Fault soft power", Flags(config.FaultSoftPowerEnable), "overcurrent"),
        new("Fault hard power", Flags(config.FaultHardPowerEnable), "overcurrent")
    ];

    private static string Number(double value, string? unit = null) => value.ToString("0.##", CultureInfo.CurrentCulture) + (unit is null ? string.Empty : " " + unit);
    private static string Pair(double first, double second, string unit) => Number(first, unit) + " / " + Number(second, unit);
    private static string Text(string? value) => string.IsNullOrWhiteSpace(value) ? "Unavailable" : value;
    private static string Flags(ushort value) => value == 0 ? "Disabled" : FaultDescriptions.Describe(value);
}

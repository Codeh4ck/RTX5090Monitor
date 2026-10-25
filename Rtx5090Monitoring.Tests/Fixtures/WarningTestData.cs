using Rtx5090Monitoring.Plugins.WireView.Analysis.Warnings;
using Rtx5090Monitoring.Plugins.Asus.Analysis.Warnings;
using Rtx5090Monitoring.Plugins.Asus.Contracts.Warnings;
using Rtx5090Monitoring.Contracts.Alerts;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Contracts.Health;
using Rtx5090Monitoring.Contracts.Sensors;
using Rtx5090Monitoring.Contracts.Warnings;

namespace Rtx5090Monitoring.Tests.Fixtures;

internal static class WarningTestData
{
    public static Warnings.WarningRegistry Registry() => new([
        new AsusWarningSource(),
        new WireViewWarningSource()]);
    public static readonly DateTimeOffset Start = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
    public static ServiceConfiguration Configuration(WarningPolicy? policy = null) => new()
    {
        Plugins = new Dictionary<string, PluginSettings> { ["asus-astral"] = new() { Options = System.Text.Json.JsonSerializer.SerializeToElement(new Rtx5090Monitoring.Plugins.Asus.Contracts.Configuration.AsusOptions { CoreTemperatureWarningC = 80 }) }, ["wireview-pro2"] = new() },
        Warnings = new WarningOptions { Defaults = policy ?? new WarningPolicy() }
    };
    public static SensorSnapshot Frame(int seconds, double temperature = 81, ushort faults = 0, AlertPin[]? pins = null)
    {
        var now = Start.AddSeconds(seconds);
        var sensors = new List<SensorReading>
        {
            new(new SensorIdentity("asus-astral", "gpu0", SensorComponent.GpuCore, SensorMetric.Temperature), SensorUnit.Celsius, temperature, now),
            new(new SensorIdentity("wireview-pro2", "com3", SensorComponent.MonitorBoard, SensorMetric.FaultStatus), SensorUnit.Bitmask, faults, now)
        };
        for (var pin = 1; pin <= 6; pin++)
        {
            sensors.Add(new SensorReading(new SensorIdentity("asus-astral", "gpu0", SensorComponent.GpuPin, SensorMetric.Current, pin), SensorUnit.Amperes, 5, now));
            sensors.Add(new SensorReading(new SensorIdentity("asus-astral", "gpu0", SensorComponent.GpuPin, SensorMetric.Voltage, pin), SensorUnit.Volts, 12, now));
        }
        return new SensorSnapshot(1, seconds + 1, now, [.. sensors],
            [new PluginHealth("asus-astral", PluginState.Ready, now, null), new PluginHealth("wireview-pro2", PluginState.Ready, now, null)], [])
        {
            Alerts = pins is null ? [] : [new SensorAlert("asus-astral", "gpu0", AsusAlertKinds.WireOvercurrent, now, pins)]
        };
    }
}

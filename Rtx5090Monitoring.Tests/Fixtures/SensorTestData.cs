using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Contracts.Sensors;
using Rtx5090Monitoring.Core;
using Rtx5090Monitoring.Contracts.Monitoring;
using Rtx5090Monitoring.Core.Plugins;
using Rtx5090Monitoring.Tests.TestDoubles;

namespace Rtx5090Monitoring.Tests.Fixtures;

internal static class SensorTestData
{
    public static readonly DateTimeOffset Timestamp = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    public static readonly SensorIdentity WireViewPower = new("wireview-pro2", "com3", SensorComponent.GpuConnector, SensorMetric.Power, Channel: 1);

    public static readonly SensorIdentity AsusPower = new("asus-astral", "gpu0", SensorComponent.GpuConnector, SensorMetric.Power, Channel: 1);

    public static SensorReading CreatePowerReading(SensorIdentity identity, double value = 400) => new(identity, SensorUnit.Watts, value, Timestamp);

    public static SensorStateStore CreateStore()
    {
        var wireViewPlugin = new TestSensorPlugin(WireViewPower.PluginId);
        var asusPlugin = new TestSensorPlugin(AsusPower.PluginId);

        return new SensorStateStore([wireViewPlugin, asusPlugin]);
    }

    public static SensorStateStore CreatePopulatedStore()
    {
        var store = CreateStore();
        var wireViewReading = CreatePowerReading(WireViewPower);
        var asusReading = CreatePowerReading(AsusPower, value: 405);

        store.Update(WireViewPower.PluginId, new PluginSample(PluginState.Ready, [wireViewReading]), Timestamp);

        store.Update(AsusPower.PluginId, new PluginSample(PluginState.Ready, [asusReading]), Timestamp);

        return store;
    }

    public static SensorSelection CreatePowerSelection(bool preferAsus = false, bool allowFallback = true) =>
        new()
        {
            Name = "gpu.connector.power",
            AllowFallback = allowFallback,
            PreferredSources = preferAsus ? [AsusPower, WireViewPower] : [WireViewPower, AsusPower]
        };
}
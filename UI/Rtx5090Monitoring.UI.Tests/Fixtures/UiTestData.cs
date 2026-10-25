using Rtx5090Monitoring.Plugins.Asus.Contracts.Warnings;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Contracts.Discovery;
using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Contracts.Health;
using Rtx5090Monitoring.Contracts.Sensors;
using Rtx5090Monitoring.Contracts.Warnings;

namespace Rtx5090Monitoring.UI.Tests.Fixtures;

internal static class UiTestData
{
    public static readonly SensorIdentity Pin = new("asus-astral", "gpu0", SensorComponent.GpuPin, SensorMetric.Current, 1);
    public static readonly PluginInfo Plugin = new("asus-astral", "ROG Astral", true, PluginState.Ready, null, null, [PluginCapability.Telemetry])
    {
        Warnings = [new WarningDefinition(AsusWarningIds.CoreTemperature, "asus-astral", "Core temperature", "temperature")]
    };
    public static readonly ConfigurationSnapshot Configuration = new(1, new ServiceConfiguration
    {
        Plugins = new Dictionary<string, PluginSettings> { ["asus-astral"] = new() }
    }, Version: "test:1");

    public static SensorSnapshot Frame(double value = 5, long sequence = 1, DateTimeOffset? timestamp = null)
    {
        var now = timestamp ?? DateTimeOffset.UtcNow;
        return new SensorSnapshot(1, sequence, now, [new SensorReading(Pin, SensorUnit.Amperes, value, now)],
            [new PluginHealth("asus-astral", PluginState.Ready, now, null)], [], 1);
    }
}

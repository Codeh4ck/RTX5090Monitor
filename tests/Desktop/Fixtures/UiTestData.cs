using RTXLens.Plugins.Asus.Contracts.Warnings;
using RTXLens.Contracts.Configuration;
using RTXLens.Contracts.Discovery;
using RTXLens.Contracts.Enums;
using RTXLens.Contracts.Health;
using RTXLens.Contracts.Sensors;
using RTXLens.Contracts.Warnings;

namespace RTXLens.UI.Tests.Fixtures;

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

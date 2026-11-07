using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Core.Plugins;

namespace Rtx5090Monitoring.Tests.TestDoubles;

internal sealed class TestSensorPluginFactory(string id, TestPluginBehavior behavior = TestPluginBehavior.PublishReadings) : ISensorPluginFactory
{
    public string Id => id;
    public string Name => id;

    public ISensorPlugin Create(PluginSettings settings)
    {
        var plugin = new TestSensorPlugin(id, behavior);
        plugin.ApplySettings(settings);
        return plugin;
    }
}

using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Contracts.Sensors;
using Rtx5090Monitoring.Core.Plugins;
using Rtx5090Monitoring.Contracts.Configuration;

namespace Rtx5090Monitoring.Tests.TestDoubles;

internal sealed class TestSensorPlugin(string pluginId, TestPluginBehavior behavior = TestPluginBehavior.PublishReadings) : ISensorPlugin
{
    public PluginDescriptor Descriptor { get; private set; } = new(pluginId, pluginId, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5));

    public void ApplySettings(PluginSettings settings) =>
        Descriptor = Descriptor with
        {
            PollInterval = TimeSpan.FromMilliseconds(settings.PollIntervalMs),
            StaleAfter = TimeSpan.FromMilliseconds(settings.StaleAfterMs)
        };

    public async ValueTask<PluginSample> PollAsync(CancellationToken cancellationToken)
    {
        switch (behavior)
        {
            case TestPluginBehavior.WaitForCancellation:
                await Task.Delay(Timeout.Infinite, cancellationToken);
                break;
            case TestPluginBehavior.ThrowOnPoll:
                throw new InvalidOperationException("Simulated provider failure.");
        }

        var identity = new SensorIdentity(Descriptor.Id, "device", SensorComponent.GpuConnector, SensorMetric.Power, Channel: 1);
        var reading = new SensorReading(identity, SensorUnit.Watts, 400, DateTimeOffset.UtcNow);

        return new PluginSample(PluginState.Ready, [reading]);
    }

    public void Dispose() { }
}

using Rtx5090Monitoring.Plugins.WireView.Analysis.Warnings;
using Rtx5090Monitoring.Warnings;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Core.Plugins;
using Rtx5090Monitoring.Plugins.WireView.Device;
using Rtx5090Monitoring.Plugins.WireView.Streaming;
using Rtx5090Monitoring.Contracts.Discovery;

namespace Rtx5090Monitoring.Plugins.WireView.Factories;

public sealed class WireViewPluginFactory(TimeProvider clock, WireViewStream stream, ILogger<WireViewPlugin> logger) : ISensorPluginFactory
{
    public IWarningSource WarningSource { get; } = new WireViewWarningSource();
    public string Id => WireViewPlugin.PluginId;
    public string Name => "WireView Pro II";
    public PluginCapability[] Capabilities => [PluginCapability.Telemetry, PluginCapability.Configuration,
        PluginCapability.Restart, PluginCapability.DeviceConfigurationRead, PluginCapability.FaultClear];

    public ISensorPlugin Create(PluginSettings settings) =>
        new WireViewPlugin(settings, clock, new WireViewProReader(logger), stream, watchApplication: true);
}

using Rtx5090Monitoring.Plugins.Asus.Analysis.Warnings;
using Rtx5090Monitoring.Warnings;
using Rtx5090Monitoring.Hardware.Nvidia;
using Rtx5090Monitoring.Plugins.Asus.Contracts.Configuration;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Core.Plugins;
using Rtx5090Monitoring.Plugins.Asus.Configuration;
using Rtx5090Monitoring.Plugins.Asus.Device;
using Rtx5090Monitoring.Plugins.Asus.Streaming;
using Rtx5090Monitoring.Plugins.Asus.Serialization;
using Rtx5090Monitoring.Contracts.Discovery;

namespace Rtx5090Monitoring.Plugins.Asus.Factories;

public sealed class AsusPluginFactory(TimeProvider clock, AsusStream stream, NvidiaTelemetryReader reader) : ISensorPluginFactory
{
    public IWarningSource WarningSource { get; } = new AsusWarningSource();
    public string Id => AsusPlugin.PluginId;
    public string Name => "ASUS RTX ROG Astral GPU";
    public PluginCapability[] Capabilities => [PluginCapability.Telemetry, PluginCapability.Configuration,
        PluginCapability.Restart, PluginCapability.DeviceSelection];

    public PluginSettings CreateDefaultSettings() => AsusSettings.WithOptions(new PluginSettings(), new AsusOptions());

    public JsonElement ReadBootstrapOptions(IConfigurationSection section) => JsonSerializer.SerializeToElement(
        section.Get<AsusOptions>(binding => binding.ErrorOnUnknownConfiguration = true) ?? new AsusOptions(), AsusJsonContext.Default.AsusOptions);

    public void ValidateSettings(PluginSettings settings) => AsusSettings.GetOptions(settings);
    public ISensorPlugin Create(PluginSettings settings) => new AsusPlugin(settings, clock, new AsusGpuReader(reader), stream);
}

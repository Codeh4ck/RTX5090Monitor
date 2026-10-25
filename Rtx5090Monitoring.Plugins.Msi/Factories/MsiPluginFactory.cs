using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Contracts.Discovery;
using Rtx5090Monitoring.Core.Plugins;
using Rtx5090Monitoring.Plugins.Msi.Configuration;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.Msi.Device;
using Rtx5090Monitoring.Plugins.Msi.Serialization;

namespace Rtx5090Monitoring.Plugins.Msi.Factories;

public sealed class MsiPluginFactory(TimeProvider clock, ILoggerFactory loggerFactory) : ISensorPluginFactory
{
    public string Id => MsiPlugin.PluginId;
    public string Name => "MSI MPG Ai1300TS / Ai1600TS";
    public PluginCapability[] Capabilities => [PluginCapability.Telemetry, PluginCapability.Configuration, PluginCapability.Restart, PluginCapability.DeviceSelection];
    public PluginSettings CreateDefaultSettings() => MsiSettings.WithOptions(new PluginSettings(), new MsiOptions());
    public void ValidateSettings(PluginSettings settings) => MsiSettings.GetOptions(settings);
    public ISensorPlugin Create(PluginSettings settings) => new MsiPlugin(settings, clock, new MsiPsuReader(loggerFactory.CreateLogger<MsiPsuReader>()));
    public JsonElement ReadBootstrapOptions(IConfigurationSection section) => JsonSerializer.SerializeToElement(
        section.Get<MsiOptions>(binding => binding.ErrorOnUnknownConfiguration = true) ?? new MsiOptions(), MsiJsonContext.Default.MsiOptions);
}
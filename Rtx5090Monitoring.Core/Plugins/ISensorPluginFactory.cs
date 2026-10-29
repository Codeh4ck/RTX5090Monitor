using Rtx5090Monitoring.Warnings;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Contracts.Discovery;

namespace Rtx5090Monitoring.Core.Plugins;

public interface ISensorPluginFactory
{
    IWarningSource? WarningSource => null;
    string Id { get; }
    string Name { get; }
    PluginCapability[] Capabilities => [PluginCapability.Telemetry, PluginCapability.Configuration, PluginCapability.Restart];
    ServiceConfiguration MigrateConfiguration(ServiceConfiguration configuration) => configuration;

    ISensorPlugin Create(PluginSettings settings);
    PluginSettings CreateDefaultSettings() => new();

    JsonElement ReadBootstrapOptions(IConfigurationSection section) =>
        throw new ArgumentException($"Plugin '{Id}' does not support additional options.");

    void ValidateSettings(PluginSettings settings)
    {
        if (settings.Options is not null)
        {
            throw new ArgumentException($"Plugin '{Id}' does not support additional options.");
        }
    }
}

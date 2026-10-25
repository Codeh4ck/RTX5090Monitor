using System.Text.Json;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Contracts.Sensors;
using Rtx5090Monitoring.Contracts.Warnings;

namespace Rtx5090Monitoring.Warnings;

public interface IWarningSource
{
    string PluginId { get; }
    IReadOnlyList<WarningDefinition> Definitions { get; }
    WarningObservation[] Observe(SensorSnapshot snapshot, PluginSettings settings);
    PluginSettings MigrateSettings(PluginSettings settings, IDictionary<string, JsonElement> legacySettings) => settings;
}

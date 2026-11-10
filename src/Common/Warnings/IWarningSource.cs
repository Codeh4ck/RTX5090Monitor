using System.Text.Json;
using RTXLens.Contracts.Configuration;
using RTXLens.Contracts.Sensors;
using RTXLens.Contracts.Warnings;

namespace RTXLens.Warnings;

public interface IWarningSource
{
    string PluginId { get; }
    IReadOnlyList<WarningDefinition> Definitions { get; }
    WarningObservation[] Observe(SensorSnapshot snapshot, PluginSettings settings);
    PluginSettings MigrateSettings(PluginSettings settings, IDictionary<string, JsonElement> legacySettings) => settings;
}

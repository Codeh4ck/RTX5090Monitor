using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Contracts.Warnings;

namespace Rtx5090Monitoring.Warnings;

public sealed class WarningRegistry
{
    public IReadOnlyList<IWarningSource> Sources { get; }
    public IReadOnlyList<WarningDefinition> Definitions { get; }

    public WarningRegistry(IEnumerable<IWarningSource> sources)
    {
        Sources = [.. sources];
        Definitions = [.. Sources.SelectMany(source => source.Definitions)];

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var aliases = new HashSet<string>(StringComparer.Ordinal);

        if (Sources.Select(source => source.PluginId).Distinct(StringComparer.Ordinal).Count() != Sources.Count)
        {
            throw new ArgumentException("Warning sources must have unique plugin IDs.");
        }

        if (Sources.Any(source => source.Definitions.Any(definition => definition.PluginId != source.PluginId
                                                                       || string.IsNullOrWhiteSpace(source.PluginId)
                                                                       || !definition.Id.StartsWith(source.PluginId + "/", StringComparison.Ordinal)
                                                                       || !ids.Add(definition.Id)
                                                                       || string.IsNullOrWhiteSpace(definition.Title)
                                                                       || definition.LegacyIds.Any(alias => string.IsNullOrWhiteSpace(alias) || !aliases.Add(alias)))))
        {
            throw new ArgumentException("Invalid or duplicated plugin warning definition.");
        }

        if (aliases.Overlaps(ids))
        {
            throw new ArgumentException("Legacy warning IDs cannot shadow current IDs.");
        }
    }

    public ServiceConfiguration Migrate(ServiceConfiguration configuration)
    {
        var warnings = configuration.Warnings ?? new WarningOptions();
        if (!warnings.IsValid())
        {
            return configuration;
        }

        var overrides = new Dictionary<string, WarningPolicy>(warnings.Overrides, StringComparer.Ordinal);
        foreach (var definition in Definitions)
        {
            foreach (var alias in definition.LegacyIds)
            {
                if (overrides.Remove(alias, out var policy))
                {
                    overrides.TryAdd(definition.Id, policy);
                }
            }
        }

        var legacy = warnings.AdditionalSettings is null
            ? new Dictionary<string, System.Text.Json.JsonElement>()
            : new Dictionary<string, System.Text.Json.JsonElement>(warnings.AdditionalSettings, StringComparer.OrdinalIgnoreCase);

        var plugins = new Dictionary<string, PluginSettings>(configuration.Plugins, StringComparer.Ordinal);

        foreach (var source in Sources)
        {
            if (plugins.TryGetValue(source.PluginId, out var settings))
            {
                plugins[source.PluginId] = source.MigrateSettings(settings, legacy);
            }
        }

        return configuration with
        {
            Plugins = plugins, Warnings = warnings with { Overrides = overrides, AdditionalSettings = legacy.Count == 0 ? null : legacy }
        };
    }
}
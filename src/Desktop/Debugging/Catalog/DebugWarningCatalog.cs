using RTXLens.Contracts.Warnings;
using RTXLens.Plugins.GenericNvidia.Analysis.Warnings;
using RTXLens.Plugins.Asus.Analysis.Warnings;
using RTXLens.Plugins.WireView.Analysis.Warnings;
using RTXLens.Warnings;

namespace RTXLens.Debugging.Catalog;

public static class DebugWarningCatalog
{
    public static WarningRegistry Registry { get; } = new([new GenericNvidiaWarningSource(), new AsusWarningSource(), new WireViewWarningSource()]);
    public static WarningDefinition[] ForPlugin(string pluginId) => [.. Registry.Definitions.Where(definition => definition.PluginId == pluginId)];
}

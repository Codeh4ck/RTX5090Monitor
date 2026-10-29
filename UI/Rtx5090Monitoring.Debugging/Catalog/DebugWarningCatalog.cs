using Rtx5090Monitoring.Contracts.Warnings;
using Rtx5090Monitoring.Plugins.GenericNvidia.Analysis.Warnings;
using Rtx5090Monitoring.Plugins.Asus.Analysis.Warnings;
using Rtx5090Monitoring.Plugins.WireView.Analysis.Warnings;
using Rtx5090Monitoring.Warnings;

namespace Rtx5090Monitoring.Debugging.Catalog;

public static class DebugWarningCatalog
{
    public static WarningRegistry Registry { get; } = new([new GenericNvidiaWarningSource(), new AsusWarningSource(), new WireViewWarningSource()]);
    public static WarningDefinition[] ForPlugin(string pluginId) => [.. Registry.Definitions.Where(definition => definition.PluginId == pluginId)];
}

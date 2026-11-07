namespace Rtx5090Monitoring.Contracts.Discovery;

public sealed record PluginCatalog(int SchemaVersion, PluginInfo[] Plugins);

namespace RTXLens.Contracts.Discovery;

public sealed record PluginCatalog(int SchemaVersion, PluginInfo[] Plugins);

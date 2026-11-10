namespace RTXLens.Core.Plugins;

internal sealed record PluginStoreEntry(PluginDescriptor Descriptor, PluginSample Sample, DateTimeOffset? LastSuccessAt);

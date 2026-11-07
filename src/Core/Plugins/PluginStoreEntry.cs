namespace Rtx5090Monitoring.Core.Plugins;

internal sealed record PluginStoreEntry(PluginDescriptor Descriptor, PluginSample Sample, DateTimeOffset? LastSuccessAt);

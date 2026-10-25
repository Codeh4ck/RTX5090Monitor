namespace Rtx5090Monitoring.Core.Plugins;

public sealed record PluginDescriptor(string Id, string Name, TimeSpan PollInterval, TimeSpan StaleAfter);
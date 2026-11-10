namespace RTXLens.Core.Plugins;

public sealed record PluginDescriptor(string Id, string Name, TimeSpan PollInterval, TimeSpan StaleAfter);
using RTXLens.Contracts.Enums;

namespace RTXLens.Contracts.Health;

public sealed record PluginHealth(string PluginId, PluginState State, DateTimeOffset? LastSuccessAt, string? Message);
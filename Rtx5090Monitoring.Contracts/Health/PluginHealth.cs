using Rtx5090Monitoring.Contracts.Enums;

namespace Rtx5090Monitoring.Contracts.Health;

public sealed record PluginHealth(string PluginId, PluginState State, DateTimeOffset? LastSuccessAt, string? Message);
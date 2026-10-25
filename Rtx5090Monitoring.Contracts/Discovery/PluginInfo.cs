using Rtx5090Monitoring.Contracts.Enums;

namespace Rtx5090Monitoring.Contracts.Discovery;

public sealed record PluginInfo(string Id, string Name, bool Enabled, PluginState State,
    DateTimeOffset? LastSuccessAt, string? Message, PluginCapability[] Capabilities)
{
    public Warnings.WarningDefinition[] Warnings { get; init; } = [];
}

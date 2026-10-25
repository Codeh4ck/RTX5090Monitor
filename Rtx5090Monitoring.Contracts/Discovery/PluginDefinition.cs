namespace Rtx5090Monitoring.Contracts.Discovery;

public sealed record PluginDefinition(string Id, string Name, PluginCapability[] Capabilities)
{
    public Warnings.WarningDefinition[] Warnings { get; init; } = [];
}

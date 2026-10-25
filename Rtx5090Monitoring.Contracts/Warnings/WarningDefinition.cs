namespace Rtx5090Monitoring.Contracts.Warnings;

public sealed record WarningDefinition(string Id, string PluginId, string Title, string Icon = "warning")
{
    public string[] LegacyIds { get; init; } = [];
}

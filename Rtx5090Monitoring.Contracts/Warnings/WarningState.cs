namespace Rtx5090Monitoring.Contracts.Warnings;

public sealed record WarningState(string Id, string PluginId, string DeviceId, string TypeId, string Title, string Detail,
    WarningPhase Phase, string EpisodeId, DateTimeOffset StartedAt, WarningPolicy Policy, long NotificationSequence,
    DateTimeOffset? LastNotificationAt)
{
    public string Icon { get; init; } = "warning";
}

using Rtx5090Monitoring.Contracts.Warnings;

namespace Rtx5090Monitoring.Warnings.State;

internal sealed class WarningEntry(WarningObservation observation, WarningDefinition definition, WarningPolicy policy, DateTimeOffset now)
{
    public WarningObservation Observation { get; set; } = observation;
    public WarningPolicy Policy { get; set; } = policy;
    public string EpisodeId { get; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset StartedAt { get; } = now;
    public WarningPhase Phase { get; set; }
    public DateTimeOffset? ClearSince { get; set; }
    public DateTimeOffset? LastNotificationAt { get; set; }
    public long NotificationSequence { get; set; }

    public WarningState Snapshot() => new(Observation.Id, Observation.PluginId, Observation.DeviceId, Observation.TypeId,
        definition.Title, Observation.Detail, Phase, EpisodeId, StartedAt, Policy, NotificationSequence, LastNotificationAt) { Icon = definition.Icon };
}

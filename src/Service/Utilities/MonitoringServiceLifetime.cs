namespace Rtx5090Monitoring.Service.Utilities;

public sealed class MonitoringServiceLifetime(TimeProvider clock)
{
    public DateTimeOffset StartedAt { get; } = clock.GetUtcNow();
}
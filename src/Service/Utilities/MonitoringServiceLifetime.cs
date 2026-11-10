namespace RTXLens.Service.Utilities;

public sealed class MonitoringServiceLifetime(TimeProvider clock)
{
    public DateTimeOffset StartedAt { get; } = clock.GetUtcNow();
}
namespace Rtx5090Monitoring.Presentation.Performance;

public sealed record PerformanceChartFrame(string SessionId, DateTimeOffset? StartedAt, double DurationSeconds,
    bool Recording, long SampleCount, string Status, PerformanceSeries[] Series)
{
    public bool DisplayOnly { get; init; }
}

namespace Rtx5090Monitoring.Presentation.Performance;

public sealed record PerformanceViewport(double Start = 0, double End = 60, int Width = 1000,
    bool Follow = true, bool Fit = false, string[]? Keys = null);

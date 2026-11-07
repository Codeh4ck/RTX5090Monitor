namespace Rtx5090Monitoring.Presentation.Performance;

public sealed record PerformanceSeries(PerformanceTrace Trace, bool Selected, bool Available, int Offset, int Count, PerformanceSample[] Samples);

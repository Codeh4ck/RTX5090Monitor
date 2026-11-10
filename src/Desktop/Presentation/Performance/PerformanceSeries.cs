namespace RTXLens.Presentation.Performance;

public sealed record PerformanceSeries(PerformanceTrace Trace, bool Selected, bool Available, int Offset, int Count, PerformanceSample[] Samples);

namespace Rtx5090Monitoring.Presentation.Performance;

public sealed record PerformanceHover(string SessionId, long RequestId, double Seconds, Dictionary<string, double?> Values)
{
    public string Type => "hover";
}

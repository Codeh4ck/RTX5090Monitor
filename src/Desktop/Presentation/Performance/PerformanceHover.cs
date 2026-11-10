namespace RTXLens.Presentation.Performance;

public sealed record PerformanceHover(string SessionId, long RequestId, double Seconds, Dictionary<string, double?> Values)
{
    public string Type => "hover";
}

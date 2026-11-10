using System.Text.Json;

namespace RTXLens.Presentation.Performance;

public static class PerformanceHtmlExporter
{
    private static readonly Lazy<string> Template = new(() =>
    {
        using var stream = typeof(PerformanceHtmlExporter).Assembly.GetManifestResourceStream(
            "RTXLens.PerformanceChart.html")
            ?? throw new InvalidOperationException("The performance chart template is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    public static string EmptyChart() => Template.Value.Replace("/*SESSION_DATA*/null", "null", StringComparison.Ordinal);

    public static string Export(PerformanceChartFrame session)
    {
        if (session.DisplayOnly || session.Recording || session.SampleCount == 0)
        {
            throw new ArgumentException("Export requires a completed recording with samples.", nameof(session));
        }

        // The default JSON encoder escapes HTML-sensitive characters in source and device names.
        var json = JsonSerializer.Serialize(session, PerformanceChartJsonContext.Default.PerformanceChartFrame);
        return Template.Value.Replace("/*SESSION_DATA*/null", json, StringComparison.Ordinal);
    }

    public static string SerializeFrame(PerformanceChartFrame frame) =>
        JsonSerializer.Serialize(frame, PerformanceChartJsonContext.Default.PerformanceChartFrame);
}

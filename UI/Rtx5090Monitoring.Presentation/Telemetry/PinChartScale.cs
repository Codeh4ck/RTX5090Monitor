namespace Rtx5090Monitoring.Presentation.Telemetry;

public sealed record PinChartScale(double Minimum, double Maximum, double Baseline, string Unit, int Intervals)
{
    public static PinChartScale For(PinMetric metric, double cableRatingW = 600) => metric switch
    {
        PinMetric.Current => new PinChartScale(0, 10, 0, "A", 5),
        PinMetric.Power => new PinChartScale(0, cableRatingW / 6, 0, "W", 4),
        _ => new PinChartScale(11.4, 12.6, 12, "V", 4)
    };

    public double Position(double value) => Math.Clamp((value - Minimum) / (Maximum - Minimum), 0, 1);
}

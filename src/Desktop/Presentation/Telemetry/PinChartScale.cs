using RTXLens.Contracts.Configuration;

namespace RTXLens.Presentation.Telemetry;

public sealed record PinChartScale(double Minimum, double Maximum, double Baseline, string Unit, int Intervals)
{
    public static PinChartScale For(PinMetric metric, double? cableRatingW = null, SettingsProfile? profile = null) => metric switch
    {
        PinMetric.Current => new PinChartScale(0, profile?.CurrentCeiling ?? 10, 0, "A", profile?.CurrentCeiling == 6 ? 6 : 5),
        PinMetric.Power => new PinChartScale(0, profile?.ResolvePowerCeiling(cableRatingW) ?? (cableRatingW ?? 600) / 6, 0, "W", 4),
        _ => new PinChartScale(profile?.VoltageMinimum ?? 11.4, profile?.VoltageMaximum ?? 12.6, 12, "V", 4)
    };

    public double Position(double value) => Math.Clamp((value - Minimum) / (Maximum - Minimum), 0, 1);
}

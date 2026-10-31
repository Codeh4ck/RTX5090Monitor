using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Contracts.Sensors;
using Rtx5090Monitoring.Presentation.Infrastructure;
using Rtx5090Monitoring.Presentation.Settings;

namespace Rtx5090Monitoring.Presentation.Telemetry;

public sealed class MsiPanelViewModel : ObservableObject
{
    private string? _device;
    public MsiConnectorViewModel[] Connectors { get; } = [new(1), new(2)];
    public MetricViewModel Temperature { get; } = new("PSU temperature");
    public MetricViewModel FanSpeed { get; } = new("PSU fan");
    public MetricViewModel FanMode { get; } = new("Fan mode");
    public MetricViewModel FanDuty { get; } = new("Requested fan duty");
    public bool ShowPeakHold
    {
        get;
        private set => Set(ref field, value);
    }

    public void Update(SensorReading[] readings, AppPreferences preferences, DateTimeOffset now, int staleAfterMs)
    {
        var valid = readings.Where(reading => reading is { Quality: ReadingQuality.Valid, Value: { } value } && double.IsFinite(value)
            && now - reading.ObservedAt <= TimeSpan.FromMilliseconds(staleAfterMs)).ToArray();
        if (valid.Select(reading => reading.Identity.DeviceId).Distinct().Skip(1).Any())
        {
            valid = [];
        }
        var device = valid.FirstOrDefault()?.Identity.DeviceId;
        if (device != _device)
        {
            foreach (var pin in Connectors.SelectMany(connector => connector.Pins))
            {
                pin.ClearPeakHistory();
            }

            _device = device;
        }
        ShowPeakHold = preferences.ShowPeakHold;
        foreach (var connector in Connectors)
        {
            connector.Update(valid, preferences);
        }

        var temperature = Value(SensorMetric.Temperature);
        if (temperature is not null && preferences.Fahrenheit)
        {
            temperature = temperature * 9 / 5 + 32;
        }

        Temperature.Value = Format(temperature, preferences.Fahrenheit ? "°F" : "°C");
        FanSpeed.Value = Format(Value(SensorMetric.FanSpeed), "RPM");
        FanDuty.Value = Format(Value(SensorMetric.FanDuty), "%");
        FanMode.Value = Value(SensorMetric.FanAutomatic) switch { 1 => "Automatic", 0 => "Manual", _ => "Unavailable" };
        return;

        double? Value(SensorMetric metric) => valid.FirstOrDefault(reading => reading.Identity.Component == SensorComponent.Psu && reading.Identity.Metric == metric)?.Value;
    }

    public void ResetPeaks()
    {
        foreach (var pin in Connectors.SelectMany(connector => connector.Pins))
        {
            pin.ResetPeak();
        }
    }

    private static string Format(double? value, string unit) => value is { } number
        ? number.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture) + " " + unit : "Unavailable";
}

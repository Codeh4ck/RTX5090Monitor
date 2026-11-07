using System.Globalization;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Presentation.Infrastructure;

namespace Rtx5090Monitoring.Presentation.Telemetry;

public sealed class PinViewModel(int channel) : ObservableObject
{
    private double? _currentPeak;
    private double? _powerPeak;
    private double? _voltagePeak;
    private double? _limit;
    private PinMetric _metric;
    private bool _showPeak;
    private double? _cableRatingW;
    private SettingsProfile? _profile;

    public int Channel { get; } = channel;
    public string Label => $"P{Channel}";
    public double? Current { get; private set; }
    public double? Voltage { get; private set; }
    public double? Power { get; private set; }
    public double? Value => _metric switch { PinMetric.Current => Current, PinMetric.Power => Power, _ => Voltage };
    public string Unit => _metric switch { PinMetric.Current => "A", PinMetric.Power => "W", _ => "V" };
    public PinChartScale Scale { get; private set; } = PinChartScale.For(PinMetric.Current);
    public string DisplayValue => Value is { } value ? $"{value.ToString("0.##", CultureInfo.CurrentCulture)} {Unit}" : "—";
    public bool IsOverflow => Value is { } value && (value < Scale.Minimum || value > Scale.Maximum);
    public double BarHeight => Value is { } value ? Scale.Position(value) * 190 : 0;
    public double BarTop => 190 - BarHeight;
    public double BaselineTop => (1 - Scale.Position(Scale.Baseline)) * 190;
    public double PeakTop => (1 - Scale.Position(Peak ?? Scale.Baseline)) * 190;
    public bool ShowPeak => _showPeak && Peak is not null;
    public bool ShowHeadroom { get; private set; }
    public double? PsuCurrent { get; private set; }
    public bool ShowPsuCurrent { get; private set; }
    public string PsuCurrentText => PsuCurrent is { } current ? current.ToString("0.##", CultureInfo.CurrentCulture) + " A" : "—";
    public bool IsWarning => _limit is { } limit && Current >= limit || Voltage is > 0 && Voltage <= (_profile?.LowPinVoltage ?? 11.5)
        || Voltage >= (_profile?.HighPinVoltage ?? 12.5);
    public string Headroom => _limit is { } limit && Current is { } current
        ? $"{(limit - current).ToString("+0.##;-0.##;0", CultureInfo.CurrentCulture)} A" : "—";
    public StatusTone HeadroomTone => _limit is not { } limit || Current is not { } current ? StatusTone.Neutral
        : current >= limit ? StatusTone.Negative : current >= limit * 0.9 ? StatusTone.Caution : StatusTone.Positive;
    private double? Peak => _metric switch { PinMetric.Current => _currentPeak, PinMetric.Power => _powerPeak, _ => _voltagePeak };

    public void SetPsuCurrent(bool visible, double? current)
    {
        if (ShowPsuCurrent == visible && PsuCurrent == current)
        {
            return;
        }

        ShowPsuCurrent = visible;
        PsuCurrent = current;
        Raise(nameof(ShowPsuCurrent));
        Raise(nameof(PsuCurrent));
        Raise(nameof(PsuCurrentText));
    }

    public void Update(double? current, double? voltage, double? power, PinMetric metric, double? limit, bool showPeak, bool showHeadroom, double? cableRatingW = null, SettingsProfile? profile = null)
    {
        cableRatingW = cableRatingW is > 0 and < double.PositiveInfinity ? cableRatingW : null;
        var scaleChanged = _metric != metric || _cableRatingW != cableRatingW || _profile != profile;
        var changed = Current != current || Voltage != voltage || Power != power || _limit != limit
            || _showPeak != showPeak || ShowHeadroom != showHeadroom || scaleChanged;
        if (scaleChanged)
        {
            Scale = PinChartScale.For(metric, cableRatingW, profile);
        }

        Current = current;
        Voltage = voltage;
        Power = power;
        _metric = metric;
        _limit = limit;
        _showPeak = showPeak;
        ShowHeadroom = showHeadroom;
        _cableRatingW = cableRatingW;
        _profile = profile;
        _currentPeak = MaximumValue(_currentPeak, current);
        _voltagePeak = MaximumValue(_voltagePeak, voltage);
        _powerPeak = MaximumValue(_powerPeak, power);
        if (changed)
        {
            Raise(string.Empty);
        }
    }

    public void ResetPeak()
    {
        _currentPeak = Current;
        _voltagePeak = Voltage;
        _powerPeak = Power;
        Raise(string.Empty);
    }

    public void ClearPeakHistory()
    {
        _currentPeak = _voltagePeak = _powerPeak = null;
        Raise(string.Empty);
    }

    private static double? MaximumValue(double? previous, double? value) => value is null ? previous : Math.Max(previous ?? value.Value, value.Value);
}

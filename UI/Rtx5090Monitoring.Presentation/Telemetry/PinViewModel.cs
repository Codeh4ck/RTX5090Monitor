using System.Globalization;
using Rtx5090Monitoring.Presentation.Infrastructure;

namespace Rtx5090Monitoring.Presentation.Telemetry;

public sealed class PinViewModel(int channel) : ObservableObject
{
    private double? _current;
    private double? _voltage;
    private double? _power;
    private double? _currentPeak;
    private double? _powerPeak;
    private double? _voltagePeak;
    private double? _limit;
    private PinMetric _metric;
    private bool _showPeak;
    private bool _showHeadroom;
    private double? _psuCurrent;
    private bool _showPsuCurrent;
    private double _cableRatingW = 600;
    private PinChartScale _scale = PinChartScale.For(PinMetric.Current);

    public int Channel { get; } = channel;
    public string Label => $"P{Channel}";
    public double? Current => _current;
    public double? Voltage => _voltage;
    public double? Power => _power;
    public double? Value => _metric switch { PinMetric.Current => _current, PinMetric.Power => _power, _ => _voltage };
    public string Unit => _metric switch { PinMetric.Current => "A", PinMetric.Power => "W", _ => "V" };
    public PinChartScale Scale => _scale;
    public string DisplayValue => Value is { } value ? $"{value.ToString("0.##", CultureInfo.CurrentCulture)} {Unit}" : "—";
    public double BarHeight => Value is { } value ? Scale.Position(value) * 190 : 0;
    public double BarTop => 190 - BarHeight;
    public double BaselineTop => (1 - Scale.Position(Scale.Baseline)) * 190;
    public double PeakTop => (1 - Scale.Position(Peak ?? Scale.Baseline)) * 190;
    public bool ShowPeak => _showPeak && Peak is not null;
    public bool ShowHeadroom => _showHeadroom;
    public double? PsuCurrent => _psuCurrent;
    public bool ShowPsuCurrent => _showPsuCurrent;
    public string PsuCurrentText => _psuCurrent is { } current ? current.ToString("0.##", CultureInfo.CurrentCulture) + " A" : "—";
    public bool IsWarning => _limit is { } limit && _current >= limit || _voltage is > 0 and <= 11.5 or >= 12.5;
    public string Headroom => _limit is { } limit && _current is { } current
        ? $"{(limit - current).ToString("+0.##;-0.##;0", CultureInfo.CurrentCulture)} A" : "—";
    public StatusTone HeadroomTone => _limit is not { } limit || _current is not { } current ? StatusTone.Neutral
        : current >= limit ? StatusTone.Negative : current >= limit * 0.9 ? StatusTone.Caution : StatusTone.Positive;
    private double? Peak => _metric switch { PinMetric.Current => _currentPeak, PinMetric.Power => _powerPeak, _ => _voltagePeak };

    public void SetPsuCurrent(bool visible, double? current)
    {
        if (_showPsuCurrent == visible && _psuCurrent == current)
        {
            return;
        }

        _showPsuCurrent = visible;
        _psuCurrent = current;
        Raise(nameof(ShowPsuCurrent));
        Raise(nameof(PsuCurrent));
        Raise(nameof(PsuCurrentText));
    }

    public void Update(double? current, double? voltage, double? power, PinMetric metric, double? limit, bool showPeak, bool showHeadroom, double cableRatingW = 600)
    {
        var changed = _current != current || _voltage != voltage || _power != power || _metric != metric || _limit != limit
            || _showPeak != showPeak || _showHeadroom != showHeadroom || _cableRatingW != cableRatingW;
        if (_metric != metric || _cableRatingW != cableRatingW)
        {
            _scale = PinChartScale.For(metric, double.IsFinite(cableRatingW) && cableRatingW > 0 ? cableRatingW : 600);
        }

        _current = current;
        _voltage = voltage;
        _power = power;
        _metric = metric;
        _limit = limit;
        _showPeak = showPeak;
        _showHeadroom = showHeadroom;
        _cableRatingW = double.IsFinite(cableRatingW) && cableRatingW > 0 ? cableRatingW : 600;
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
        _currentPeak = _current;
        _voltagePeak = _voltage;
        _powerPeak = _power;
        Raise(string.Empty);
    }

    public void ClearPeakHistory()
    {
        _currentPeak = _voltagePeak = _powerPeak = null;
        Raise(string.Empty);
    }

    private static double? MaximumValue(double? previous, double? value) => value is null ? previous : Math.Max(previous ?? value.Value, value.Value);
}

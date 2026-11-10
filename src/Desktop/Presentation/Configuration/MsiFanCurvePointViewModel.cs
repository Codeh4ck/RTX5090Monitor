using RTXLens.Plugins.Msi.Contracts.Configuration;
using RTXLens.Presentation.Infrastructure;

namespace RTXLens.Presentation.Configuration;

public sealed class MsiFanCurvePointViewModel(double temperatureC, double dutyPercent) : EditableObject
{
    private double _temperatureC = Round(temperatureC);
    private double _dutyPercent = NormalizeDuty(dutyPercent);

    public double TemperatureC { get => _temperatureC; set => Edit(ref _temperatureC, Round(value)); }
    public double DutyPercent { get => _dutyPercent; set => Edit(ref _dutyPercent, NormalizeDuty(value)); }

    public MsiFanCurvePoint Build() => new(NumericInput.Integer(TemperatureC, "Fan curve temperature"),
        NumericInput.Integer(DutyPercent, "Fan curve duty"));

    private static double NormalizeDuty(double value)
    {
        var rounded = Round(value);
        return rounded is >= 0 and < 25 ? 0 : rounded;
    }

    private static double Round(double value) => double.IsFinite(value) ? Math.Round(value, MidpointRounding.AwayFromZero) : value;
}

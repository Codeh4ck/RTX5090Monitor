using Rtx5090Monitoring.Plugins.Msi.Contracts.Configuration;

namespace Rtx5090Monitoring.Plugins.Msi.Control;

internal sealed class MsiFanCurveController
{
    private int _duty;
    private DateTimeOffset? _changeSince;

    public int Evaluate(MsiFanCurve curve, double temperatureC, DateTimeOffset now)
    {
        if (!double.IsFinite(temperatureC) || temperatureC is < 0 or > 125)
        {
            throw new IOException("The PSU temperature cannot be used for fan control.");
        }

        var requested = MsiFanCurve.EffectiveDutyPercent(Interpolate(curve.Points, temperatureC));

        if (requested == _duty)
        {
            _changeSince = null;
            return _duty;
        }

        // Keep the timer running as temperature changes; apply the latest target when due.
        _changeSince ??= now;

        if (now - _changeSince >= TimeSpan.FromSeconds(curve.ResponseDelaySeconds))
        {
            _duty = requested;
            _changeSince = null;
        }

        return _duty;
    }

    public void Reset(int appliedDuty = 0)
    {
        _duty = MsiFanCurve.EffectiveDutyPercent(appliedDuty);
        _changeSince = null;
    }

    internal static int Interpolate(MsiFanCurvePoint[] points, double temperatureC)
    {
        if (temperatureC <= points[0].TemperatureC)
        {
            return points[0].DutyPercent;
        }

        for (var index = 1; index < points.Length; index++)
        {
            var upper = points[index];

            if (temperatureC > upper.TemperatureC)
            {
                continue;
            }

            var lower = points[index - 1];
            var fraction = (temperatureC - lower.TemperatureC) / (upper.TemperatureC - lower.TemperatureC);

            return (int)Math.Round(lower.DutyPercent + fraction * (upper.DutyPercent - lower.DutyPercent), MidpointRounding.AwayFromZero);
        }

        return points[^1].DutyPercent;
    }
}

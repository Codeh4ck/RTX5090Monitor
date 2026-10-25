using Rtx5090Monitoring.Plugins.Asus.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.Asus.Contracts.Common;

namespace Rtx5090Monitoring.Plugins.Asus.Analysis.Monitoring;

public sealed class AsusAlertMonitor
{
    private readonly Dictionary<string, DateTimeOffset> _overcurrentSince = new(StringComparer.Ordinal);
    private DateTimeOffset? _imbalanceSince;

    public CurrentImbalance? CurrentImbalance { get; private set; }
    public WireOvercurrent? WireOvercurrent { get; private set; }
    public double? ImbalancePercentage { get; private set; }

    public void Evaluate(IReadOnlyDictionary<string, float> pins, AsusOptions options, DateTimeOffset now)
    {
        EvaluateImbalance(pins, options, now);
        EvaluateOvercurrent(pins, options, now);
    }

    public void Reset()
    {
        _overcurrentSince.Clear();
        _imbalanceSince = null;
        CurrentImbalance = null;
        WireOvercurrent = null;
        ImbalancePercentage = null;
    }

    private void EvaluateImbalance(IReadOnlyDictionary<string, float> pins, AsusOptions options, DateTimeOffset now)
    {
        CurrentImbalance = null;
        ImbalancePercentage = null;
        if (pins.Count < 2)
        {
            _imbalanceSince = null;
            return;
        }

        var maximum = pins.MaxBy(pin => pin.Value);
        var minimum = pins.MinBy(pin => pin.Value);
        var average = (maximum.Value + minimum.Value) / 2d;
        var difference = average > 0 ? Math.Abs(maximum.Value - minimum.Value) / average * 100 : 0;

        ImbalancePercentage = difference;

        if (maximum.Value < options.ImbalanceAmperageThreshold || difference < options.ImbalancePercentageThreshold)
        {
            _imbalanceSince = null;
            return;
        }

        _imbalanceSince ??= now;

        if (now - _imbalanceSince.Value >= TimeSpan.FromMilliseconds(options.ImbalanceDurationThresholdMs))
        {
            CurrentImbalance = new CurrentImbalance
            {
                MaxPinAmperage = maximum.Value,
                MaxPinName = maximum.Key,
                MinPinAmperage = minimum.Value,
                MinPinName = minimum.Key,
                PercentageDifference = (int)Math.Round(difference),
                Timestamp = now
            };
        }
    }

    private void EvaluateOvercurrent(IReadOnlyDictionary<string, float> pins, AsusOptions options, DateTimeOffset now)
    {
        foreach (var name in _overcurrentSince.Keys.ToArray())
        {
            if (!pins.TryGetValue(name, out var value) || value < options.WireOvercurrentAmperageThreshold)
            {
                _overcurrentSince.Remove(name);
            }
        }

        var detected = new Dictionary<string, float>();
        foreach (var (name, value) in pins)
        {
            if (value < options.WireOvercurrentAmperageThreshold)
            {
                continue;
            }

            _overcurrentSince.TryAdd(name, now);
            if (now - _overcurrentSince[name] >= TimeSpan.FromMilliseconds(options.WireOvercurrentThresholdMs))
            {
                detected[name] = value;
            }
        }

        WireOvercurrent = detected.Count > 0 ? new WireOvercurrent { PinValues = detected } : null;
    }
}

using Rtx5090Monitoring.Plugins.Asus.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.Asus.Analysis.Monitoring;
using Rtx5090Monitoring.Tests.Infrastructure;

namespace Rtx5090Monitoring.Tests.Core;

internal static class AsusAlertTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly AsusOptions Options = new();

    public static void Run(TestRunner runner)
    {
        runner.Run(WaitsForSustainedImbalance);
        runner.Run(ClearsImbalanceOnRecoveryAtHighLoad);
        runner.Run(RestartsImbalanceDurationAfterRecovery);
        runner.Run(TracksEachOvercurrentPinIndependently);
        runner.Run(DoesNotTransferOvercurrentDurationBetweenPins);
        runner.Run(ResetsMissingPinAndDisconnectedDeviceWindows);
        runner.Run(DoesNotReportUnavailableCurrentsAsHealthy);
    }

    private static void WaitsForSustainedImbalance()
    {
        var monitor = new AsusAlertMonitor();
        var pins = new Dictionary<string, float> { ["Pin 1"] = 8, ["Pin 2"] = 4 };

        monitor.Evaluate(pins, Options, Start);
        monitor.Evaluate(pins, Options, Start.AddMilliseconds(2999));
        TestAssert.True(monitor.CurrentImbalance is null, "Imbalance must persist for the configured duration.");

        monitor.Evaluate(pins, Options, Start.AddMilliseconds(3000));
        TestAssert.Equal("Pin 1", monitor.CurrentImbalance!.MaxPinName);
        TestAssert.Equal("Pin 2", monitor.CurrentImbalance.MinPinName);
        TestAssert.Equal(67, monitor.CurrentImbalance.PercentageDifference);
    }

    private static void ClearsImbalanceOnRecoveryAtHighLoad()
    {
        var monitor = new AsusAlertMonitor();
        var imbalance = new Dictionary<string, float> { ["Pin 1"] = 8, ["Pin 2"] = 4 };
        monitor.Evaluate(imbalance, Options, Start);
        monitor.Evaluate(imbalance, Options, Start.AddSeconds(3));

        var balanced = new Dictionary<string, float> { ["Pin 1"] = 8, ["Pin 2"] = 8 };
        monitor.Evaluate(balanced, Options, Start.AddSeconds(4));

        TestAssert.True(monitor.CurrentImbalance is null, "Recovery must clear the warning even above the activation amperage.");
        TestAssert.Equal<double?>(0, monitor.ImbalancePercentage);
    }

    private static void RestartsImbalanceDurationAfterRecovery()
    {
        var monitor = new AsusAlertMonitor();
        var imbalance = new Dictionary<string, float> { ["Pin 1"] = 8, ["Pin 2"] = 4 };
        var balanced = new Dictionary<string, float> { ["Pin 1"] = 8, ["Pin 2"] = 8 };
        monitor.Evaluate(imbalance, Options, Start);
        monitor.Evaluate(balanced, Options, Start.AddSeconds(2));
        monitor.Evaluate(imbalance, Options, Start.AddSeconds(3));
        monitor.Evaluate(imbalance, Options, Start.AddSeconds(5));

        TestAssert.True(monitor.CurrentImbalance is null, "A new imbalance needs its own uninterrupted window.");
        monitor.Evaluate(imbalance, Options, Start.AddSeconds(6));
        TestAssert.True(monitor.CurrentImbalance is not null, "The new sustained imbalance should be reported.");
    }

    private static void TracksEachOvercurrentPinIndependently()
    {
        var monitor = new AsusAlertMonitor();
        monitor.Evaluate(new Dictionary<string, float> { ["Pin 1"] = 10 }, Options, Start);
        var both = new Dictionary<string, float> { ["Pin 1"] = 10, ["Pin 2"] = 10 };
        monitor.Evaluate(both, Options, Start.AddSeconds(2));
        monitor.Evaluate(both, Options, Start.AddSeconds(3));

        TestAssert.Equal(1, monitor.WireOvercurrent!.PinValues.Count);
        TestAssert.True(monitor.WireOvercurrent.PinValues.ContainsKey("Pin 1"), "Only the first pin has met its duration.");
        monitor.Evaluate(both, Options, Start.AddSeconds(5));
        TestAssert.Equal(2, monitor.WireOvercurrent!.PinValues.Count);
    }

    private static void DoesNotTransferOvercurrentDurationBetweenPins()
    {
        var monitor = new AsusAlertMonitor();
        monitor.Evaluate(new Dictionary<string, float> { ["Pin 1"] = 10 }, Options, Start);
        var secondPin = new Dictionary<string, float> { ["Pin 1"] = 6, ["Pin 2"] = 10 };
        monitor.Evaluate(secondPin, Options, Start.AddSeconds(2));
        monitor.Evaluate(secondPin, Options, Start.AddSeconds(3));

        TestAssert.True(monitor.WireOvercurrent is null, "A different offending pin must not inherit elapsed time.");
        monitor.Evaluate(secondPin, Options, Start.AddSeconds(5));
        TestAssert.Equal("Pin 2", monitor.WireOvercurrent!.PinValues.Keys.Single());
    }

    private static void ResetsMissingPinAndDisconnectedDeviceWindows()
    {
        var monitor = new AsusAlertMonitor();
        var pins = new Dictionary<string, float> { ["Pin 1"] = 10, ["Pin 2"] = 4 };
        monitor.Evaluate(pins, Options, Start);
        monitor.Evaluate(new Dictionary<string, float>(), Options, Start.AddSeconds(2));
        monitor.Evaluate(pins, Options, Start.AddSeconds(3));
        TestAssert.True(monitor.WireOvercurrent is null, "Missing samples must interrupt a pin's duration.");

        monitor.Evaluate(pins, Options, Start.AddSeconds(6));
        monitor.Reset();
        monitor.Evaluate(pins, Options, Start.AddSeconds(7));
        TestAssert.True(monitor.CurrentImbalance is null && monitor.WireOvercurrent is null,
            "Reconnection must start fresh detection windows.");
    }

    private static void DoesNotReportUnavailableCurrentsAsHealthy()
    {
        var monitor = new AsusAlertMonitor();
        monitor.Evaluate(new Dictionary<string, float>(), Options, Start);

        TestAssert.True(monitor.ImbalancePercentage is null, "An absent measurement is not a zero-percent imbalance.");
        TestAssert.True(monitor.CurrentImbalance is null && monitor.WireOvercurrent is null, "Unavailable samples cannot trigger alerts.");
    }
}

using Rtx5090Monitoring.Contracts.Alerts;
using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Contracts.Sensors;
using Rtx5090Monitoring.Presentation.Notifications;
using Rtx5090Monitoring.Presentation.Settings;
using Rtx5090Monitoring.Presentation.Telemetry;
using Rtx5090Monitoring.Tests.Infrastructure;
using Rtx5090Monitoring.UI.Tests.Fixtures;

namespace Rtx5090Monitoring.UI.Tests.Telemetry;

internal static class TelemetryTests
{
    public static void Run(TestRunner runner)
    {
        runner.Run(PinHeightUsesAFixedUpwardScale);
        runner.Run(MissingValuesNeverDisplayAsZero);
        runner.Run(PeakHistoryCanBeClearedAcrossDevices);
        runner.Run(StaleReadingsAreRemovedFromThePanel);
        runner.Run(MissingWireViewFaultsAreUnavailable);
        runner.Run(VoltageBarsFillUpwardFromTheAxisMinimum);
        runner.Run(PowerRangeFollowsCableRating);
        runner.Run(PinValuesUseAtMostTwoDecimals);
    }

    private static void PinHeightUsesAFixedUpwardScale()
    {
        var pin = new PinViewModel(1);
        pin.Update(5, 12, 60, PinMetric.Current, 9.2, true, true);
        TestAssert.Equal(95d, pin.BarHeight);
        TestAssert.Equal(95d, pin.PeakTop);
        pin.Update(20, 12, 240, PinMetric.Current, 9.2, true, true);
        TestAssert.Equal(190d, pin.BarHeight);
        TestAssert.Equal(0d, pin.PeakTop);
    }

    private static void VoltageBarsFillUpwardFromTheAxisMinimum()
    {
        var pin = new PinViewModel(1);
        pin.Update(1, 12, 12, PinMetric.Voltage, 9.2, false, false);
        TestAssert.Equal(95d, pin.BarHeight);
        TestAssert.Equal(95d, pin.BarTop);
        TestAssert.Equal(95d, pin.BaselineTop);
        pin.Update(1, 12.6, 12.6, PinMetric.Voltage, 9.2, false, false);
        TestAssert.Equal(190d, pin.BarHeight);
        TestAssert.Equal(0d, pin.BarTop);
        pin.Update(1, 11.4, 11.4, PinMetric.Voltage, 9.2, false, false);
        TestAssert.Equal(0d, pin.BarHeight);
        TestAssert.Equal(190d, pin.BarTop);
        pin.Update(null, null, null, PinMetric.Voltage, null, false, false);
        TestAssert.Equal(0d, pin.BarHeight);
        TestAssert.Equal(190d, pin.BarTop);
    }

    private static void PowerRangeFollowsCableRating()
    {
        var pin = new PinViewModel(1);
        pin.Update(5, 12, 60, PinMetric.Power, null, false, false);
        TestAssert.Equal(100d, pin.Scale.Maximum);
        TestAssert.Equal(114d, pin.BarHeight);
        pin.Update(5, 12, 25, PinMetric.Power, null, false, false, 300);
        TestAssert.Equal(50d, pin.Scale.Maximum);
        TestAssert.Equal(95d, pin.BarHeight);
        pin.Update(5, 12, 75, PinMetric.Power, null, false, false, 300);
        TestAssert.Equal(190d, pin.BarHeight);
        TestAssert.Equal("75 W", pin.DisplayValue);
    }

    private static void PinValuesUseAtMostTwoDecimals()
    {
        var pin = new PinViewModel(1);
        pin.Update(9.199999809, 12, 110.4, PinMetric.Current, 10, false, true);
        TestAssert.Equal(9.2.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture) + " A", pin.DisplayValue);
        pin.Update(5, 12, 60, PinMetric.Current, 10, false, true);
        TestAssert.Equal("5 A", pin.DisplayValue);
        TestAssert.Equal("+5 A", pin.Headroom);
    }

    private static void MissingValuesNeverDisplayAsZero()
    {
        var pin = new PinViewModel(1);
        pin.Update(null, null, null, PinMetric.Power, null, true, true);
        TestAssert.Equal("—", pin.DisplayValue);
        TestAssert.Equal("—", pin.Headroom);
        TestAssert.False(pin.ShowPeak, "No peak should be invented for an absent reading.");
    }

    private static void PeakHistoryCanBeClearedAcrossDevices()
    {
        var pin = new PinViewModel(1);
        pin.Update(10, 12, 120, PinMetric.Current, 9.2, true, true);
        pin.ClearPeakHistory();
        pin.Update(2, 12, 24, PinMetric.Current, 9.2, true, true);
        TestAssert.True(pin.PeakTop > 150, "The previous device's peak must not survive.");
    }

    private static void StaleReadingsAreRemovedFromThePanel()
    {
        var panel = new HardwarePanelViewModel(UiTestData.Plugin);
        var now = DateTimeOffset.UtcNow;
        panel.Apply(UiTestData.Frame(timestamp: now), new AppPreferences(), now);
        TestAssert.Equal(5d, panel.Pins[0].Current);
        panel.MarkExpired(now.AddSeconds(6), new AppPreferences());
        TestAssert.Equal("Stale", panel.State);
        TestAssert.Equal<double?>(null, panel.Pins[0].Current);
    }

    private static void MissingWireViewFaultsAreUnavailable()
    {
        var frame = UiTestData.Frame();
        var panel = new HardwarePanelViewModel(UiTestData.Plugin with { Id = "wireview-pro2" });
        panel.Apply(frame with { Plugins = [frame.Plugins[0] with { PluginId = "wireview-pro2" }] }, new AppPreferences(), frame.Timestamp);
        TestAssert.Equal("Unavailable", panel.Metrics.Single(metric => metric.Label == "Fault Status").Value);
    }

}

using System.Globalization;
using Rtx5090Monitoring.Contracts.Discovery;
using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Contracts.Sensors;
using Rtx5090Monitoring.Presentation.Settings;
using Rtx5090Monitoring.Presentation.Telemetry;
using Rtx5090Monitoring.Tests.Infrastructure;
using Rtx5090Monitoring.UI.Tests.Fixtures;

namespace Rtx5090Monitoring.UI.Tests.Telemetry;

internal static class HardwarePanelTests
{
    public static void Run(TestRunner runner)
    {
        runner.Run(TracksSelectionIndependentlyOfVisualContainers);
        runner.Run(DiscoveryDoesNotSelectHardwareOverAnotherPage);
        runner.Run(DescribesCurrentSpreadAndItsPins);
        runner.Run(ColorsReadyWarningAndErrorStatusesSemantically);
        runner.Run(HeadroomReportsLimitAndMissingValues);
        runner.Run(PeakResetVisibilityFollowsSettings);
        runner.Run(OnlyReportingPluginsHaveTabs);
        runner.Run(DisabledPluginsDoNotRaiseMissingDeviceWarnings);
        runner.Run(StaleOrEmptyPluginsLoseTheirTabs);
        runner.Run(GroupsGpuStatusAndPromotesHeadlineMetrics);
        runner.Run(UnavailablePinFlagsDoNotCreateAnEmptyChart);
    }

    private static void TracksSelectionIndependentlyOfVisualContainers()
    {
        var dashboard = new DashboardViewModel();
        dashboard.SetCatalog(new PluginCatalog(1, [UiTestData.Plugin, UiTestData.Plugin with { Id = "wireview-pro2" }]));
        dashboard.SelectedPanel = dashboard.Panels[0];
        TestAssert.True(dashboard.Panels[0].IsSelected, "A newly rendered tab must inherit active selection.");
        dashboard.SelectedPanel = dashboard.Panels[1];
        TestAssert.False(dashboard.Panels[0].IsSelected, "The previous tab must lose selection.");
        TestAssert.True(dashboard.Panels[1].IsSelected, "The active tab must stay selected.");
    }

    private static void DiscoveryDoesNotSelectHardwareOverAnotherPage()
    {
        var dashboard = new DashboardViewModel();
        var catalog = new PluginCatalog(1, [UiTestData.Plugin]);
        dashboard.SetCatalog(catalog);
        dashboard.SelectedPanel = dashboard.Panels[0];
        dashboard.SelectedPanel = null;
        dashboard.SetCatalog(catalog);
        TestAssert.False(dashboard.Panels[0].IsSelected, "Configuration/settings navigation must remain unselected after metadata refresh.");
    }

    private static void DescribesCurrentSpreadAndItsPins()
    {
        var frame = UiTestData.Frame();
        frame = frame with { Sensors = [frame.Sensors[0] with { Value = 4 }, new SensorReading(UiTestData.Pin with { Channel = 6 }, SensorUnit.Amperes, 7.5, frame.Timestamp)] };
        var panel = new HardwarePanelViewModel(UiTestData.Plugin);
        panel.Apply(frame, new AppPreferences(), frame.Timestamp);
        TestAssert.Equal(3.5.ToString("0.##", CultureInfo.CurrentCulture) + " A", panel.CurrentSpreadText);
        TestAssert.True(panel.HighestCurrentText.StartsWith("P6", StringComparison.Ordinal), "The highest pin must be explicit.");
        TestAssert.True(panel.LowestCurrentText.StartsWith("P1", StringComparison.Ordinal), "The lowest pin must be explicit.");
        panel.MarkDisconnected(new AppPreferences());
        TestAssert.Equal("—", panel.CurrentSpreadText);
    }

    private static void ColorsReadyWarningAndErrorStatusesSemantically()
    {
        TestAssert.Equal(StatusTone.Positive, new HardwarePanelViewModel(UiTestData.Plugin).Tone);
        TestAssert.Equal(StatusTone.Caution, new HardwarePanelViewModel(UiTestData.Plugin with { State = PluginState.Disabled }).Tone);
        TestAssert.Equal(StatusTone.Caution, new HardwarePanelViewModel(UiTestData.Plugin with { State = PluginState.Stale }).Tone);
        TestAssert.Equal(StatusTone.Negative, new HardwarePanelViewModel(UiTestData.Plugin with { State = PluginState.Error }).Tone);
    }

    private static void HeadroomReportsLimitAndMissingValues()
    {
        var pin = new PinViewModel(1);
        pin.Update(5, 12, 60, PinMetric.Current, 9.2, false, true);
        TestAssert.Equal(StatusTone.Positive, pin.HeadroomTone);
        pin.Update(10, 12, 120, PinMetric.Current, 9.2, false, true);
        TestAssert.Equal(StatusTone.Negative, pin.HeadroomTone);
        pin.Update(null, null, null, PinMetric.Current, 9.2, false, true);
        TestAssert.Equal(StatusTone.Neutral, pin.HeadroomTone);
        TestAssert.Equal("—", pin.Headroom);
    }

    private static void PeakResetVisibilityFollowsSettings()
    {
        var panel = new HardwarePanelViewModel(UiTestData.Plugin);
        var frame = UiTestData.Frame();
        panel.Apply(frame, new AppPreferences(), frame.Timestamp);
        TestAssert.False(panel.ShowPeakHold, "Reset should be hidden by default.");
        panel.Apply(frame, new AppPreferences { ShowPeakHold = true }, frame.Timestamp);
        TestAssert.True(panel.ShowPeakHold, "Reset should be available when peak hold is enabled.");
    }

    private static void OnlyReportingPluginsHaveTabs()
    {
        var dashboard = new DashboardViewModel();
        dashboard.SetCatalog(new PluginCatalog(1, [UiTestData.Plugin, UiTestData.Plugin with { Id = "wireview-pro2", State = PluginState.NoDevice }]));
        var frame = UiTestData.Frame();
        dashboard.Apply(frame, new AppPreferences(), frame.Timestamp);
        TestAssert.Equal(2, dashboard.Panels.Count);
        TestAssert.Equal("asus-astral", dashboard.ReportingPanels.Single().Id);
        TestAssert.True(dashboard.PluginWarning.Contains("WireView", StringComparison.Ordinal), "Missing enabled hardware must have a named warning.");
        dashboard.SelectedPanel = dashboard.ReportingPanels[0];
        dashboard.Apply(frame with { Plugins = [frame.Plugins[0] with { State = PluginState.NoDevice }] }, new AppPreferences(), frame.Timestamp);
        TestAssert.Equal(0, dashboard.ReportingPanels.Count);
        TestAssert.Equal<HardwarePanelViewModel?>(null, dashboard.SelectedPanel);
        dashboard.Apply(frame, new AppPreferences(), frame.Timestamp);
        TestAssert.Equal(1, dashboard.ReportingPanels.Count);
    }

    private static void DisabledPluginsDoNotRaiseMissingDeviceWarnings()
    {
        var dashboard = new DashboardViewModel();
        dashboard.SetCatalog(new PluginCatalog(1, [UiTestData.Plugin with { Enabled = false, State = PluginState.Disabled }]));
        var frame = UiTestData.Frame();
        dashboard.Apply(frame, new AppPreferences(), frame.Timestamp);
        TestAssert.Equal(0, dashboard.ReportingPanels.Count);
        TestAssert.False(dashboard.HasPluginWarning, "Disabled plugins must be hidden without warnings even if the last frame was Ready.");
    }

    private static void StaleOrEmptyPluginsLoseTheirTabs()
    {
        var dashboard = new DashboardViewModel();
        dashboard.SetCatalog(new PluginCatalog(1, [UiTestData.Plugin]));
        var frame = UiTestData.Frame();
        dashboard.Apply(frame with { Sensors = [] }, new AppPreferences(), frame.Timestamp);
        TestAssert.Equal(0, dashboard.ReportingPanels.Count);
        dashboard.Apply(frame, new AppPreferences(), frame.Timestamp);
        TestAssert.Equal(1, dashboard.ReportingPanels.Count);
        dashboard.RefreshFreshness(frame.Timestamp.AddSeconds(6), new AppPreferences());
        TestAssert.Equal(0, dashboard.ReportingPanels.Count);
        TestAssert.True(dashboard.HasPluginWarning, "Stale plugins must be named in the warning.");
    }

    private static void GroupsGpuStatusAndPromotesHeadlineMetrics()
    {
        var frame = UiTestData.Frame();
        frame = frame with { Sensors = [
            Reading(SensorComponent.GpuFan, SensorMetric.FanSpeed, 1200, 1), Reading(SensorComponent.GpuFan, SensorMetric.FanSpeed, 1400, 2),
            Reading(SensorComponent.GpuCore, SensorMetric.Temperature, 85), Reading(SensorComponent.GpuMemory, SensorMetric.Temperature, 70),
            Reading(SensorComponent.GpuMemory, SensorMetric.MemoryUsed, 8192), Reading(SensorComponent.GpuMemory, SensorMetric.MemoryTotal, 32768),
            Reading(SensorComponent.GpuMemory, SensorMetric.Utilization, 25), Reading(SensorComponent.GpuCore, SensorMetric.Utilization, 80),
            Reading(SensorComponent.GpuBoard, SensorMetric.Power, 450)] };
        frame = frame with
        {
            Sensors = [.. frame.Sensors.Select(reading => reading with { Identity = reading.Identity with { PluginId = "generic-nvidia" } })],
            Plugins = [new Rtx5090Monitoring.Contracts.Health.PluginHealth("generic-nvidia", PluginState.Ready, frame.Timestamp, "RTX 5090")]
        };
        var model = new GpuStatusViewModel();
        model.Apply(frame, null, new AppPreferences(), frame.Timestamp);
        TestAssert.Equal(5, model.Metrics.Count);
        TestAssert.Equal("VRAM usage", model.Metrics[2].Label);
        TestAssert.Equal("Core / VRAM temp", model.Metrics[3].Label);
        TestAssert.Equal("1200 RPM / 1400 RPM", model.Metrics.Single(metric => metric.Label == "Fan 1 / 2").Value);
        TestAssert.Equal("80%", model.GpuUsageText);
        TestAssert.False(model.Metrics[3].IsWarning, "Warning colors must consume the shared backend state, not infer independent thresholds.");
        model.Clear();
        TestAssert.True(model.IsUnavailable, "Disconnected GPU status must be explicit.");
        return;

        SensorReading Reading(SensorComponent component, SensorMetric metric, double value, int? channel = null) =>
            new(UiTestData.Pin with { Component = component, Metric = metric, Channel = channel }, SensorUnits.For(metric), value, frame.Timestamp);
    }

    private static void UnavailablePinFlagsDoNotCreateAnEmptyChart()
    {
        var frame = UiTestData.Frame();
        frame = frame with { Sensors = [
            new SensorReading(UiTestData.Pin with { Component = SensorComponent.GpuCore, Metric = SensorMetric.Clock }, SensorUnit.Megahertz, 2000, frame.Timestamp),
            new SensorReading(UiTestData.Pin with { Metric = SensorMetric.OvercurrentDetected }, SensorUnit.Boolean, null, frame.Timestamp, ReadingQuality.Unavailable)] };
        var panel = new HardwarePanelViewModel(UiTestData.Plugin);
        panel.Apply(frame, new AppPreferences(), frame.Timestamp);
        TestAssert.False(panel.HasPins, "Unavailable protection flags must not create a chart without pin readings.");
        TestAssert.True(panel.IsReporting, "General GPU telemetry must keep the Astral tab available without pin readings.");
    }
}

using System.Globalization;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Contracts.Discovery;
using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Contracts.Health;
using Rtx5090Monitoring.Contracts.Sensors;
using Rtx5090Monitoring.Presentation.Settings;
using Rtx5090Monitoring.Presentation.Telemetry;
using Rtx5090Monitoring.Tests.Infrastructure;

namespace Rtx5090Monitoring.UI.Tests.Telemetry;

internal static class PsuComparisonTests
{
    private static readonly AppPreferences BothEnabled = new()
    {
        ShowWireViewPsuCurrent = true,
        ShowAsusPsuCurrent = true,
        WireViewPsuConnector = 1,
        AsusPsuConnector = 2,
        PinMetric = PinMetric.Voltage
    };

    public static void Run(TestRunner runner)
    {
        runner.Run(MapsConnectorsIndependentlyWithoutChangingChartUnits);
        runner.Run(DefaultsOffAndHonorsIndependentToggles);
        runner.Run(HidesComparisonOnSourceLossAndRecovers);
        runner.Run(ExpiresIndividualPinsWithoutReusingOldValues);
        runner.Run(DisablingSourceClearsComparisonImmediately);
        runner.Run(RejectsInvalidOrAmbiguousSourceReadings);
    }

    private static void MapsConnectorsIndependentlyWithoutChangingChartUnits()
    {
        var dashboard = CreateDashboard();
        var frame = Frame();
        dashboard.Apply(frame, BothEnabled, frame.Timestamp);
        var wire = dashboard.Panels[0];
        var asus = dashboard.Panels[1];
        TestAssert.True(wire.ShowPsuComparison && asus.ShowPsuComparison, "Both charts should show their chosen connector.");
        TestAssert.Equal<double?>(1.125, wire.Pins[0].PsuCurrent);
        TestAssert.Equal<double?>(2.125, asus.Pins[0].PsuCurrent);
        TestAssert.Equal<double?>(2.75, asus.Pins[5].PsuCurrent);
        TestAssert.Equal("V", asus.Pins[0].Unit);
        TestAssert.Equal("2.13 A".Replace('.', CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0]), asus.Pins[0].PsuCurrentText);
        TestAssert.True(asus.PsuComparisonLabel.Contains("CONNECTOR 2", StringComparison.Ordinal), "The source connector must be labeled.");
        dashboard.Apply(frame, BothEnabled with { WireViewPsuConnector = 2, AsusPsuConnector = 1 }, frame.Timestamp);
        TestAssert.Equal<double?>(2.125, wire.Pins[0].PsuCurrent);
        TestAssert.Equal<double?>(1.125, asus.Pins[0].PsuCurrent);
    }

    private static void DefaultsOffAndHonorsIndependentToggles()
    {
        var dashboard = CreateDashboard();
        var frame = Frame();
        dashboard.Apply(frame, new AppPreferences(), frame.Timestamp);
        TestAssert.False(dashboard.Panels.Any(panel => panel.ShowPsuComparison), "Existing preferences must leave comparison disabled.");
        dashboard.Apply(frame, BothEnabled with { ShowWireViewPsuCurrent = false }, frame.Timestamp);
        TestAssert.False(dashboard.Panels[0].Pins.Any(pin => pin.ShowPsuCurrent), "WireView setting must be independent.");
        TestAssert.True(dashboard.Panels[1].Pins.All(pin => pin.ShowPsuCurrent), "Astral setting should remain enabled.");
        dashboard.Apply(frame, BothEnabled with { ShowAsusPsuCurrent = false }, frame.Timestamp);
        TestAssert.False(dashboard.Panels[1].ShowPsuComparison, "Turning the setting off must hide the source row.");
    }

    private static void HidesComparisonOnSourceLossAndRecovers()
    {
        var dashboard = CreateDashboard();
        var frame = Frame();
        dashboard.Apply(frame, BothEnabled, frame.Timestamp);
        var unavailable = frame with { Plugins =
            [.. frame.Plugins.Select(plugin => plugin.PluginId == "msi-psu" ? plugin with { State = PluginState.NoDevice } : plugin)]
        };
        dashboard.Apply(unavailable, BothEnabled, frame.Timestamp);
        TestAssert.False(dashboard.Panels[1].ShowPsuComparison, "Cached currents must not survive source loss.");
        TestAssert.True(dashboard.Panels[1].Pins.All(pin => pin.PsuCurrent is null), "Lost source values must be cleared.");
        dashboard.Apply(frame, BothEnabled, frame.Timestamp);
        TestAssert.True(dashboard.Panels[1].ShowPsuComparison, "Reconnection should restore comparison automatically.");
        dashboard.Disconnect(BothEnabled);
        TestAssert.False(dashboard.Panels[1].ShowPsuComparison, "Service disconnect must clear comparison.");
    }

    private static void ExpiresIndividualPinsWithoutReusingOldValues()
    {
        var dashboard = CreateDashboard();
        var frame = Frame();
        var oldPin = frame.Sensors.Single(reading => reading.Identity is { PluginId: "msi-psu", ConnectorId: "2", Channel: 1 });
        frame = frame with { Sensors =
            [.. frame.Sensors.Select(reading => reading == oldPin ? reading with { ObservedAt = frame.Timestamp.AddMilliseconds(-900) } : reading)]
        };
        dashboard.Apply(frame, BothEnabled, frame.Timestamp);
        dashboard.RefreshFreshness(frame.Timestamp.AddMilliseconds(200), BothEnabled);
        TestAssert.True(dashboard.Panels[1].ShowPsuComparison, "Other fresh pins should keep the comparison row visible.");
        TestAssert.Equal("—", dashboard.Panels[1].Pins[0].PsuCurrentText);
        TestAssert.Equal<double?>(2.25, dashboard.Panels[1].Pins[1].PsuCurrent);
        dashboard.RefreshFreshness(frame.Timestamp.AddMilliseconds(1100), BothEnabled);
        TestAssert.False(dashboard.Panels[1].ShowPsuComparison, "The MSI freshness limit must apply independently of the GPU limit.");
    }

    private static void DisablingSourceClearsComparisonImmediately()
    {
        var dashboard = CreateDashboard();
        var frame = Frame();
        dashboard.Apply(frame, BothEnabled, frame.Timestamp);
        dashboard.SetConfiguration(new ConfigurationSnapshot(2, new ServiceConfiguration
        {
            Plugins = new Dictionary<string, PluginSettings> { ["msi-psu"] = new PluginSettings { Enabled = false } }
        }));
        TestAssert.False(dashboard.Panels[0].ShowPsuComparison, "Disabling MSI must clear comparison without waiting for a frame.");
    }

    private static void RejectsInvalidOrAmbiguousSourceReadings()
    {
        var dashboard = CreateDashboard();
        var frame = Frame();
        frame = frame with { Sensors =
            [
                .. frame.Sensors.Select(reading => reading.Identity.PluginId == "msi-psu"
                    ? reading with { Quality = ReadingQuality.Stale }
                    : reading)
            ]
        };
        dashboard.Apply(frame, BothEnabled, frame.Timestamp);
        TestAssert.False(dashboard.Panels[0].ShowPsuComparison, "Stale readings must never appear as PSU comparison values.");
        frame = Frame();
        var extraDevice = frame.Sensors.First(reading => reading.Identity.PluginId == "msi-psu");
        frame = frame with { Sensors = [.. frame.Sensors, extraDevice with { Identity = extraDevice.Identity with { DeviceId = "other-psu" } }] };
        dashboard.Apply(frame, BothEnabled, frame.Timestamp);
        TestAssert.False(dashboard.Panels[0].ShowPsuComparison, "Readings from different PSUs must not be merged.");
    }

    private static DashboardViewModel CreateDashboard()
    {
        var dashboard = new DashboardViewModel();
        dashboard.SetCatalog(new PluginCatalog(1, [
            new PluginInfo("wireview-pro2", "WireView", true, PluginState.Ready, null, null, []),
            new PluginInfo("asus-astral", "Astral", true, PluginState.Ready, null, null, []),
            new PluginInfo("msi-psu", "MSI", true, PluginState.Ready, null, null, [])]));
        dashboard.SetConfiguration(new ConfigurationSnapshot(1, new ServiceConfiguration
        {
            Plugins = new Dictionary<string, PluginSettings> { ["msi-psu"] = new PluginSettings { StaleAfterMs = 1000 } }
        }));
        return dashboard;
    }

    private static SensorSnapshot Frame()
    {
        var now = DateTimeOffset.UtcNow;
        var sensors = new List<SensorReading>();
        foreach (var plugin in new[] { "wireview-pro2", "asus-astral" })
        {
            sensors.Add(new SensorReading(new SensorIdentity(plugin, "gpu", SensorComponent.GpuPin, SensorMetric.Current, 1), SensorUnit.Amperes, 1, now));
        }

        for (var connector = 1; connector <= 2; connector++)
        {
            for (var pin = 1; pin <= 6; pin++)
            {
                sensors.Add(new SensorReading(new SensorIdentity("msi-psu", "psu", SensorComponent.PsuPin, SensorMetric.Current,
                    pin, connector.ToString(CultureInfo.InvariantCulture)), SensorUnit.Amperes, connector + pin * 0.125, now));
            }
        }

        return new SensorSnapshot(1, 1, now, [.. sensors], [
            new PluginHealth("wireview-pro2", PluginState.Ready, now, null), new PluginHealth("asus-astral", PluginState.Ready, now, null),
            new PluginHealth("msi-psu", PluginState.Ready, now, null)], []);
    }
}

using System.Text.Json;
using Rtx5090Monitoring.Client.Serialization;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Contracts.Discovery;
using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Contracts.Health;
using Rtx5090Monitoring.Contracts.Sensors;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Configuration;
using Rtx5090Monitoring.Presentation.Configuration;
using Rtx5090Monitoring.Presentation.Settings;
using Rtx5090Monitoring.Presentation.Telemetry;
using Rtx5090Monitoring.Tests.Infrastructure;
using Rtx5090Monitoring.UI.Tests.TestDoubles;

namespace Rtx5090Monitoring.UI.Tests.Telemetry;

internal static class MsiPanelTests
{
    public static void Run(TestRunner runner)
    {
        runner.Run(KeepsBothConnectorChartsDistinctAndCurrentOnly);
        runner.Run(TracksFanDraftsAndValidatesPolling);
    }

    private static void KeepsBothConnectorChartsDistinctAndCurrentOnly()
    {
        var now = DateTimeOffset.UtcNow;
        var panel = new HardwarePanelViewModel(new PluginInfo("msi-psu", "MSI PSU", true, PluginState.Ready, now, null, []));
        var sensors = new List<SensorReading>();
        for (var connector = 1; connector <= 2; connector++)
            for (var pin = 1; pin <= 6; pin++)
        {
            sensors.Add(new SensorReading(new SensorIdentity("msi-psu", "psu", SensorComponent.PsuPin, SensorMetric.Current,
                pin, connector.ToString()), SensorUnit.Amperes, connector == 1 ? pin : 0, now));
        }

        var frame = new SensorSnapshot(1, 1, now, [.. sensors], [new PluginHealth("msi-psu", PluginState.Ready, now, null)], []);
        panel.Apply(frame, new AppPreferences { PinMetric = PinMetric.Voltage }, now);
        TestAssert.Equal<double?>(1, panel.Msi.Connectors[0].Pins[0].Current);
        TestAssert.Equal<double?>(0, panel.Msi.Connectors[1].Pins[0].Current);
        TestAssert.Equal("A", panel.Msi.Connectors[0].Pins[0].Unit);
        TestAssert.False(panel.Msi.Connectors[0].Pins[0].ShowHeadroom, "No unverified protection headroom should be displayed.");
        panel.MarkDisconnected(new AppPreferences());
        TestAssert.True(panel.Msi.Connectors.All(connector => connector.Pins.All(pin => pin.Current is null)), "Disconnect must clear both connectors.");
    }

    private static void TracksFanDraftsAndValidatesPolling()
    {
        var settings = new PluginSettings { Options = JsonSerializer.SerializeToElement(new MsiOptions(), ClientJsonContext.Default.MsiOptions) };
        var editor = new ConfigurationEditorViewModel(() => new FakeMonitoringApi());
        editor.Receive(new ConfigurationSnapshot(1, new ServiceConfiguration { Plugins = new Dictionary<string, PluginSettings> { ["msi-psu"] = settings } }));
        var plugin = editor.Plugins.Single();
        plugin.Msi.FanModeIndex = (int)MsiFanControl.Manual;
        plugin.Msi.FanDutyPercent = 75;
        TestAssert.True(editor.IsDirty && plugin.Msi.IsManual, "Manual fan control must be an editable draft.");
        var built = plugin.Build().Options!.Value.Deserialize(ClientJsonContext.Default.MsiOptions)!;
        TestAssert.Equal(75, built.FanDutyPercent);
        plugin.Msi.FanModeIndex = (int)MsiFanControl.Unmanaged;
        plugin.Msi.FanDutyPercent = 50;
        TestAssert.False(editor.IsDirty, "Reverting MSI options must hide the edit footer.");
        plugin.PollIntervalMs = 500;
        TestAssert.Throws<ArgumentException>(() => plugin.Build());
        plugin.PollIntervalMs = 1000;
        plugin.Msi.FanDutyPercent = double.NaN;
        TestAssert.Throws<ArgumentException>(() => plugin.Build());
    }
}

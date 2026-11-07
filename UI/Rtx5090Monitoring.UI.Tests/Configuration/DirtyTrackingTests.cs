using Rtx5090Monitoring.Contracts.Monitoring;
using Rtx5090Monitoring.Presentation.Configuration;
using Rtx5090Monitoring.Tests.Infrastructure;
using Rtx5090Monitoring.UI.Tests.Fixtures;
using Rtx5090Monitoring.UI.Tests.TestDoubles;

namespace Rtx5090Monitoring.UI.Tests.Configuration;

internal static class DirtyTrackingTests
{
    public static void Run(TestRunner runner)
    {
        runner.Run(RevertingSharedAndPluginSettingsClearsChanges);
        runner.Run(ChangingAsusGpuPreservesLegacyThresholds);
        runner.Run(RemovingANewSelectionRestoresTheOriginalConfiguration);
        runner.Run(RestoringSourceOrderClearsChanges);
        runner.Run(AbandoningADraftAcceptsThePendingExternalConfiguration);
    }

    private static void RevertingSharedAndPluginSettingsClearsChanges()
    {
        var editor = new ConfigurationEditorViewModel(() => new FakeMonitoringApi());
        editor.Receive(UiTestData.Configuration);
        editor.StreamIntervalMs = 250;
        TestAssert.True(editor.IsDirty, "A different stream interval must count as a change.");
        editor.StreamIntervalMs = 1000;
        TestAssert.False(editor.IsDirty, "Restoring the interval must clear the change.");
        editor.Plugins[0].Enabled = false;
        TestAssert.True(editor.IsDirty, "Enablement must count as a change.");
        editor.Plugins[0].Enabled = true;
        TestAssert.False(editor.IsDirty, "Restoring enablement must clear the change.");
        editor.Plugins[0].PollIntervalMs = double.NaN;
        TestAssert.True(editor.IsDirty, "Invalid input must stay available for correction.");
        editor.Plugins[0].PollIntervalMs = 1000;
        TestAssert.False(editor.IsDirty, "Correcting invalid input back to its original value must clear the change.");
    }

    private static void ChangingAsusGpuPreservesLegacyThresholds()
    {
        var original = new Rtx5090Monitoring.Plugins.Asus.Contracts.Configuration.AsusOptions
        {
            WireOvercurrentAmperageThreshold = 9.5f,
            ImbalancePercentageThreshold = 25
        };
        var asus = new AsusSettingsEditorViewModel(original);
        asus.SetDevices([new("gpu0", "RTX 5090")]);
        asus.SelectedDevice = asus.Devices.Single(device => device.Id == "gpu0");

        TestAssert.Equal(original with { DeviceId = "gpu0" }, asus.Build());
        asus.SelectedDevice = asus.Devices.Single(device => device.Id is null);
        TestAssert.True(asus.Matches(original), "Returning to the original GPU must restore the original plugin options.");
    }

    private static void RemovingANewSelectionRestoresTheOriginalConfiguration()
    {
        var editor = new ConfigurationEditorViewModel(() => new FakeMonitoringApi());
        editor.Receive(UiTestData.Configuration);
        editor.AddSelection();
        TestAssert.True(editor.IsDirty, "Adding a selection must count as a change.");
        editor.RemoveSelection(editor.Selections.Single());
        TestAssert.False(editor.IsDirty, "Removing the new selection must restore the original draft.");
    }

    private static void RestoringSourceOrderClearsChanges()
    {
        var configuration = UiTestData.Configuration with
        {
            Configuration = UiTestData.Configuration.Configuration with
            {
                Monitoring = new MonitoringOptions
                {
                    Selections = [new SensorSelection { Name = "current", PreferredSources = [UiTestData.Pin, UiTestData.Pin with { PluginId = "wireview-pro2" }] }]
                }
            }
        };
        var editor = new ConfigurationEditorViewModel(() => new FakeMonitoringApi());
        editor.Receive(configuration);
        editor.Selections[0].Sources.Move(0, 1);
        TestAssert.True(editor.IsDirty, "Preferred-source ordering is part of configuration.");
        editor.Selections[0].Sources.Move(0, 1);
        TestAssert.False(editor.IsDirty, "Restoring the original order must clear the change.");
    }

    private static void AbandoningADraftAcceptsThePendingExternalConfiguration()
    {
        var editor = new ConfigurationEditorViewModel(() => new FakeMonitoringApi());
        editor.Receive(UiTestData.Configuration);
        editor.StreamIntervalMs = 250;
        editor.Receive(UiTestData.Configuration with
        {
            Revision = 2, Version = "test:2", Configuration = UiTestData.Configuration.Configuration with
            {
                Monitoring = new MonitoringOptions { StreamIntervalMs = 2000 }
            }
        });
        TestAssert.True(editor.HasExternalChanges, "An external revision must be tracked while editing.");
        editor.StreamIntervalMs = 1000;
        TestAssert.False(editor.IsDirty || editor.HasExternalChanges, "Restoring the draft should accept the pending service settings.");
        TestAssert.Equal(2000d, editor.StreamIntervalMs);
    }
}

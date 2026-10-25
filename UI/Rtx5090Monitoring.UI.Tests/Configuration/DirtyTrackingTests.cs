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
        runner.Run(RevertingAsusThresholdsClearsChanges);
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

    private static void RevertingAsusThresholdsClearsChanges()
    {
        var editor = new ConfigurationEditorViewModel(() => new FakeMonitoringApi());
        editor.Receive(UiTestData.Configuration);
        editor.Plugins[0].Asus.OvercurrentA = 9.5;
        TestAssert.True(editor.IsDirty, "A changed threshold must count as an edit.");
        editor.Plugins[0].Asus.OvercurrentA = 9.2;
        TestAssert.False(editor.IsDirty, "The original single-precision threshold must compare by its transmitted value.");
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

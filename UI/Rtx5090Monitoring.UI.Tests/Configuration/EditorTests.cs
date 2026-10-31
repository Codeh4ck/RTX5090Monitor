using Rtx5090Monitoring.Plugins.Asus.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.Asus.Contracts.Common;
using Rtx5090Monitoring.Presentation.Configuration;
using Rtx5090Monitoring.Tests.Infrastructure;
using Rtx5090Monitoring.UI.Tests.Fixtures;
using Rtx5090Monitoring.UI.Tests.TestDoubles;

namespace Rtx5090Monitoring.UI.Tests.Configuration;

internal static class EditorTests
{
    public static async Task RunAsync(TestRunner runner)
    {
        await runner.RunAsync(DoesNotSubmitUnchangedSettingsAsync);
        await runner.RunAsync(ConflictsPreserveUnsavedEditsAsync);
        await runner.RunAsync(AppliesTheWholeConfigurationWithItsVersionAsync);
        await runner.RunAsync(InvalidInputNeverReachesTheServiceAsync);
        runner.Run(DeviceRefreshPreservesAnExplicitMissingGpu);
        runner.Run(RefreshDoesNotDirtyTheConfiguration);
    }

    private static async Task DoesNotSubmitUnchangedSettingsAsync()
    {
        var api = new FakeMonitoringApi();
        var editor = new ConfigurationEditorViewModel(() => api);
        editor.Receive(api.Configuration);
        await editor.ApplyAsync(default);
        TestAssert.Equal(1L, api.Configuration.Revision);
    }

    private static async Task ConflictsPreserveUnsavedEditsAsync()
    {
        var api = new FakeMonitoringApi();
        var editor = new ConfigurationEditorViewModel(() => api);
        editor.Receive(api.Configuration);
        editor.StreamIntervalMs = 250;
        api.Configuration = api.Configuration with { Revision = 2, Version = "test:2" };
        editor.Receive(api.Configuration);
        TestAssert.True(editor.HasExternalChanges, "An external update must be reported.");
        await editor.ApplyAsync(default);
        TestAssert.True(editor is { IsDirty: true, HasExternalChanges: true }, "A conflict must preserve the draft.");
        TestAssert.Equal(250d, editor.StreamIntervalMs);
        await editor.ReloadAsync(default);
        TestAssert.False(editor.IsDirty || editor.HasExternalChanges, "Reload must replace the draft.");
    }

    private static async Task AppliesTheWholeConfigurationWithItsVersionAsync()
    {
        var api = new FakeMonitoringApi();
        var editor = new ConfigurationEditorViewModel(() => api);
        editor.Receive(api.Configuration);
        editor.StreamIntervalMs = 300;
        editor.Plugins[0].Enabled = false;
        await editor.ApplyAsync(default);
        TestAssert.Equal(300, api.Configuration.Configuration.Monitoring.StreamIntervalMs);
        TestAssert.False(api.Configuration.Configuration.Plugins["asus-astral"].Enabled, "Enablement must be applied live.");
        TestAssert.False(editor.IsDirty, "An acknowledged apply must clear the draft.");
    }

    private static async Task InvalidInputNeverReachesTheServiceAsync()
    {
        var api = new FakeMonitoringApi();
        var editor = new ConfigurationEditorViewModel(() => api);
        editor.Receive(api.Configuration);
        editor.StreamIntervalMs = double.NaN;
        await editor.ApplyAsync(default);
        TestAssert.Equal(1L, api.Configuration.Revision);
        TestAssert.True(editor.IsDirty, "Invalid values should remain available for correction.");
    }

    private static void DeviceRefreshPreservesAnExplicitMissingGpu()
    {
        var editor = new AsusSettingsEditorViewModel(new AsusOptions { DeviceId = "missing-gpu" });
        editor.SetDevices([new AsusDeviceInfo("gpu0", "RTX 5090")]);
        TestAssert.Equal("missing-gpu", editor.Build(1000).DeviceId);
    }

    private static void RefreshDoesNotDirtyTheConfiguration()
    {
        var editor = new ConfigurationEditorViewModel(() => new FakeMonitoringApi());
        editor.Receive(UiTestData.Configuration);
        editor.SetDevices([new AsusDeviceInfo("gpu0", "RTX 5090")]);
        TestAssert.False(editor.IsDirty, "Discovery must not change the saved device selection.");
    }
}

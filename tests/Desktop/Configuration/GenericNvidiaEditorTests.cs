using Rtx5090Monitoring.Plugins.GenericNvidia.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.GenericNvidia.Contracts.Devices;
using Rtx5090Monitoring.Presentation.Configuration;
using Rtx5090Monitoring.Tests.Infrastructure;

namespace Rtx5090Monitoring.UI.Tests.Configuration;

internal static class GenericNvidiaEditorTests
{
    public static void Run(TestRunner runner)
    {
        runner.Run(RefreshPreservesSelectionWithoutEditingTheDraft);
        runner.Run(DisabledWarningsAndTransientNumberInputRemainSafe);
    }

    private static void RefreshPreservesSelectionWithoutEditingTheDraft()
    {
        var options = new GenericNvidiaOptions { DeviceId = "/gpu-nvidia/0" };
        var editor = new GenericNvidiaSettingsEditorViewModel(options);
        var edits = 0;
        editor.Edited += () => edits++;
        editor.SetDevices([new NvidiaDeviceInfo("pci-stable", "RTX 5090", "/gpu-nvidia/0")]);
        editor.SelectedDevice = null;
        TestAssert.Equal("pci-stable", editor.SelectedDevice.Id);
        TestAssert.Equal(0, edits);
        TestAssert.True(editor.Matches(options), "Resolving a legacy device name must not dirty a draft.");
        TestAssert.Equal("pci-stable", editor.Build().DeviceId);
    }

    private static void DisabledWarningsAndTransientNumberInputRemainSafe()
    {
        var options = new GenericNvidiaOptions { CoreTemperatureWarningC = null };
        var editor = new GenericNvidiaSettingsEditorViewModel(options);
        TestAssert.True(editor.Build().CoreTemperatureWarningC is null, "A disabled threshold must round-trip.");
        editor.MemoryTemperatureWarningC = double.NaN;
        TestAssert.False(editor.Matches(options), "Blank NumberBox input must be a dirty draft, not an exception during live refresh.");
        TestAssert.Throws<ArgumentException>(() => editor.Build());
    }
}

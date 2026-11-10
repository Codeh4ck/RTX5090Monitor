using Microsoft.UI.Windowing;
using RTXLens.App.WireView.Windows;

namespace RTXLens.App.Services;

internal sealed partial class AppRuntime
{
    internal WireViewDeviceWindow? WireViewSettingsWindow { get; private set; }

    internal async Task OpenWireViewDeviceSettingsAsync()
    {
        if (WireViewSettingsWindow is null)
        {
            WireViewSettingsWindow = new WireViewDeviceWindow(this);
            WireViewSettingsWindow.Closed += (_, _) => WireViewSettingsWindow = null;
            TrackWindow(WireViewSettingsWindow, "wireview-settings", 660, 850, besideOwner: true);

        }
        var window = WireViewSettingsWindow;
        window.SetPanel(Dashboard.Panels.FirstOrDefault(panel => panel.IsWireView));
        window.AppWindow.Show();
        window.Activate();
        await window.RefreshAsync();
    }
}

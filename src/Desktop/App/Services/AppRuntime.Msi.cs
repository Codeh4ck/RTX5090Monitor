using Microsoft.UI.Windowing;
using Rtx5090Monitoring.App.Msi.Windows;

namespace Rtx5090Monitoring.App.Services;

internal sealed partial class AppRuntime
{
    internal MsiFanCurveWindow? FanCurveWindow { get; private set; }

    internal async Task OpenMsiFanCurveAsync()
    {
        var configuration = Configuration;
        var msi = configuration.Plugins.FirstOrDefault(plugin => plugin.IsMsi)?.Msi;

        if (msi?.IsCurve != true)
        {
            return;
        }

        if (FanCurveWindow is not null)
        {
            FanCurveWindow.Activate();
            return;
        }

        var window = new MsiFanCurveWindow(CurrentApi, Preferences,
            configuration.ReceiveMsiCurveUpdate,
            () =>
            {
                var current = configuration.Plugins.FirstOrDefault(plugin => plugin.IsMsi)?.Msi;

                if (current is not null)
                {
                    current.FanModeIndex = (int)Plugins.Msi.Contracts.Configuration.MsiFanControl.Automatic;
                }
            });
        FanCurveWindow = window;
        window.Closed += (_, _) => FanCurveWindow = null;
        var area = DisplayArea.GetFromWindowId(window.AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var scale = _window?.ThemeRoot.XamlRoot?.RasterizationScale ?? 1;
        window.AppWindow.Resize(new Windows.Graphics.SizeInt32(
            Math.Min(area.Width, (int)(1000 * scale)), Math.Min(area.Height, (int)(720 * scale))));
        window.Activate();
        await window.InitializeAsync();
    }
}

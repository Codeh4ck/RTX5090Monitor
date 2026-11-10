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
            if (_window is { } main)
            {
                var area = DisplayArea.GetFromWindowId(main.AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
                var scale = main.ThemeRoot.XamlRoot?.RasterizationScale ?? 1;
                var width = Math.Min(area.Width, (int)(660 * scale));
                var height = Math.Min(area.Height, (int)(850 * scale));
                WireViewSettingsWindow.AppWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
                var right = main.AppWindow.Position.X + main.AppWindow.Size.Width + 8;
                var left = main.AppWindow.Position.X - width - 8;
                var x = right + width <= area.X + area.Width ? right : left >= area.X ? left : area.X + area.Width - width;
                WireViewSettingsWindow.AppWindow.Move(new Windows.Graphics.PointInt32(x, Math.Clamp(main.AppWindow.Position.Y, area.Y, area.Y + area.Height - height)));
            }
        }
        var window = WireViewSettingsWindow;
        window.SetPanel(Dashboard.Panels.FirstOrDefault(panel => panel.IsWireView));
        window.AppWindow.Show();
        window.Activate();
        await window.RefreshAsync();
    }
}

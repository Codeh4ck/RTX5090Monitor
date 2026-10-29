using Microsoft.UI;
using Microsoft.UI.Windowing;
using Rtx5090Monitoring.Presentation.Settings;
using Rtx5090Monitoring.App.Platform.Interop;
using Windows.Graphics;

namespace Rtx5090Monitoring.App.Platform.Windowing;

internal static class WindowSizeLimits
{
    public const int MinimumWidth = 720;
    public const int MinimumHeight = 550;
    public const int MaximumWidth = 7680;
    public const int MaximumHeight = 4320;

    public static SizeInt32 GetMaximumPixels(nint window)
    {
        var scale = Math.Max(96, NativeMethods.GetDpiForWindow(window)) / 96d;
        var area = DisplayArea.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(window), DisplayAreaFallback.Primary).WorkArea;
        return new SizeInt32(Math.Min(area.Width, (int)(MaximumWidth * scale)), Math.Min(area.Height, (int)(MaximumHeight * scale)));
    }
}

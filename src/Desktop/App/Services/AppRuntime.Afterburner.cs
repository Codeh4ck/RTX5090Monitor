using Microsoft.UI.Windowing;
using Rtx5090Monitoring.App.Msi.Afterburner;
using Rtx5090Monitoring.Presentation.Msi.Afterburner;

namespace Rtx5090Monitoring.App.Services;

internal sealed partial class AppRuntime
{
    internal AfterburnerSettingsWindow? AfterburnerWindow { get; private set; }

    internal async Task<bool> IsAfterburnerInstalledAsync()
    {
        try
        {
            var selection = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Rtx5090Monitoring", "afterburner-installation.txt");
            var remembered = File.Exists(selection) ? await File.ReadAllTextAsync(selection) : null;
            return await Task.Run(() => AfterburnerDiscovery.Find(remembered).Length > 0);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException
            or ArgumentException or System.ComponentModel.Win32Exception)
        {
            System.Diagnostics.Debug.WriteLine(error);
            return false;
        }
    }

    internal async Task OpenAfterburnerSettingsAsync()
    {
        if (Configuration.Plugins.FirstOrDefault(plugin => plugin.IsMsi)?.Enabled != true
            || !await IsAfterburnerInstalledAsync())
        {
            return;
        }

        if (AfterburnerWindow is null)
        {
            var window = new AfterburnerSettingsWindow(this);
            AfterburnerWindow = window;
            window.Closed += (_, _) => AfterburnerWindow = null;
            var area = DisplayArea.GetFromWindowId(window.AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
            var scale = _window?.ThemeRoot.XamlRoot?.RasterizationScale ?? 1;
            window.AppWindow.Resize(new Windows.Graphics.SizeInt32(Math.Min(area.Width, (int)(1280 * scale)),
                Math.Min(area.Height, (int)(1000 * scale))));
        }

        AfterburnerWindow.Activate();
    }
}
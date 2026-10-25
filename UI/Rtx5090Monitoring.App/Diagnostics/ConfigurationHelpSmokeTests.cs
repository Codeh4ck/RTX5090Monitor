using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Rtx5090Monitoring.App.Services;
using Rtx5090Monitoring.App.Views;
using Rtx5090Monitoring.App.Views.Dialogs;
using Rtx5090Monitoring.Presentation.Settings;
using Windows.Foundation;

namespace Rtx5090Monitoring.App.Diagnostics;

internal static class ConfigurationHelpSmokeTests
{
    public static async Task RunAsync(MainWindow window, AppRuntime runtime, string output)
    {
        var measurements = new List<string>();
        var scale = window.ThemeRoot.XamlRoot.RasterizationScale;
        var maximum = Platform.Windowing.WindowSizeLimits.GetMaximumPixels(WinRT.Interop.WindowNative.GetWindowHandle(window));
        window.AppWindow.Resize(new Windows.Graphics.SizeInt32(Math.Min(maximum.Width, (int)(new AppPreferences().WindowWidth * scale)),
            Math.Min(maximum.Height, (int)(AppPreferences.DefaultWindowHeight * scale))));
        await Task.Delay(300);
        measurements.Add($"Window: {window.AppWindow.Size.Width / scale:F1} x {window.AppWindow.Size.Height / scale:F1} logical pixels");
        runtime.Configuration.Plugins.Single(plugin => plugin.IsMsi).Enabled = true;
        await runtime.Configuration.ApplyAsync(runtime.StopToken);
        await Task.Delay(500);
        runtime.Settings.Theme = AppTheme.Dark;
        runtime.Settings.ShowAsusPsuCurrent = true;
        runtime.Settings.ShowPeakHold = true;
        window.ShowPanel("asus-astral");
        await Task.Delay(500);
        var hardware = (HardwareView)window.CurrentView!;
        var hardwareScroll = VisualTreeQuery.Descendants<ScrollViewer>(hardware).First();
        var restart = VisualTreeQuery.Descendants<Button>(hardware).Single(button => button.Content as string == "Restart plugin");
        var restartBottom = restart.TransformToVisual(hardwareScroll).TransformPoint(new Point(0, restart.ActualHeight)).Y;
        measurements.Add($"ASUS hardware overflow: {hardwareScroll.ScrollableHeight:F1}; restart bottom: {restartBottom:F1}; viewport: {hardwareScroll.ViewportHeight:F1}");
        await SmokeTestRunner.CaptureElementAsync(window.ThemeRoot, Path.Combine(output, "asus-dashboard-default-height.png"));
        var hardwareFits = hardwareScroll.ScrollableHeight < 1 && restartBottom <= hardwareScroll.ActualHeight + 1;

        window.Navigate("configuration");
        var view = (ConfigurationView)window.CurrentView!;
        view.SelectSection("plugin:asus-astral");
        await Task.Delay(500);
        var settings = VisualTreeQuery.Descendants<PluginSettingsView>(view).Single(item => item.Model.IsAsus);
        DependencyObject? ancestor = settings;
        while (ancestor is not null && ancestor is not ScrollViewer)
        {
            ancestor = VisualTreeHelper.GetParent(ancestor);
        }
        var configurationScroll = (ScrollViewer)ancestor!;
        measurements.Add($"ASUS configuration overflow: {configurationScroll.ScrollableHeight:F1}; viewport: {configurationScroll.ViewportHeight:F1}");
        await SmokeTestRunner.CaptureElementAsync(window.ThemeRoot, Path.Combine(output, "asus-configuration-default-height.png"));
        var configurationFits = configurationScroll.ScrollableHeight < 1;
        for (var refresh = 0; refresh < 3; refresh++)
        {
            await runtime.RefreshDevicesAsync();
        }
        if (runtime.Configuration.IsDirty || settings.Model.Asus.SelectedDevice is null)
        {
            throw new InvalidOperationException("Refreshing a mounted GPU selector must preserve selection and leave configuration unchanged.");
        }
        foreach (var theme in new[] { AppTheme.Dark, AppTheme.Light })
        {
            runtime.Settings.Theme = theme;
            view.SelectSection("stream");
            await Task.Delay(250);
            var streamTab = VisualTreeQuery.Descendants<TabView>(view).Single().TabItems.OfType<TabViewItem>().Single(tab => tab.Tag as string == "stream");
            if (streamTab.Header as string != "Sensor stream")
            {
                throw new InvalidOperationException("Stream navigation must use the Sensor stream label.");
            }
            await VerifyDialogAsync(window, view, new SensorStreamHelpDialog(), "Sensor stream", "Polling and publication are separate", output, "sensor-stream-" + theme.ToString().ToLowerInvariant());
            view.SelectSection("sources");
            await VerifyDialogAsync(window, view, new SourceSelectionsHelpDialog(), "Source selections", "Example: choose your preferred power reading", output, "source-selections-" + theme.ToString().ToLowerInvariant());
            view.SelectSection("maintenance");
            await Task.Delay(250);
            var maintenance = VisualTreeQuery.Descendants<Expander>(view).Single(panel => panel.Name == "MaintenancePanel");
            maintenance.IsExpanded = true;
            await Task.Delay(250);
            var restartBackend = VisualTreeQuery.Descendants<Button>(maintenance).Single(button => button.Name == "RestartBackendServiceButton");
            var restartInfo = VisualTreeQuery.Descendants<Button>(maintenance).Single(button => button.Name == "BackendRestartInfoButton");
            if (restartBackend.Content as string != "Restart backend service" || !restartInfo.IsEnabled)
            {
                throw new InvalidOperationException("Backend maintenance must identify the restart scope and offer help.");
            }
            await VerifyDialogAsync(window, view, new BackendRestartHelpDialog(), "Restart backend service", "What restarts", output, "backend-restart-" + theme.ToString().ToLowerInvariant());
        }
        if (runtime.Configuration.IsDirty)
        {
            throw new InvalidOperationException("Reading configuration help must not modify the service configuration.");
        }
        await File.WriteAllLinesAsync(Path.Combine(output, "layout-measurements.txt"), measurements);
        if (!hardwareFits || !configurationFits)
        {
            throw new InvalidOperationException("The ASUS pages do not fit the default window height. " + string.Join("; ", measurements));
        }
        await File.WriteAllTextAsync(Path.Combine(output, "verification.txt"), "PASS: ASUS dashboard and configuration fit the app-wide default. Restart is visible. GPU refresh is safe while mounted. All three help dialogs open, scroll and close in light/dark themes without changing configuration.");
    }

    private static async Task VerifyDialogAsync(MainWindow window, ConfigurationView view, ContentDialog dialog, string title, string expectedText, string output, string name)
    {
        var pending = view.ShowHelpAsync(dialog);
        await Task.Delay(400);
        if (pending.IsCompleted)
        {
            await pending;
        }
        if (!dialog.IsLoaded)
        {
            throw new InvalidOperationException("The help dialog was not loaded: " + title);
        }
        if (dialog.Title as string != title || !VisualTreeQuery.Descendants<TextBlock>(dialog).Any(text => text.Text == expectedText))
        {
            throw new InvalidOperationException("The configuration explanation was not displayed: " + title);
        }
        var scroll = (ScrollViewer)dialog.Content;
        if (scroll.VerticalScrollBarVisibility != ScrollBarVisibility.Auto)
        {
            throw new InvalidOperationException("Configuration help must be scrollable.");
        }
        await SmokeTestRunner.CaptureElementAsync(dialog, Path.Combine(output, name + ".png"));
        dialog.Hide();
        await pending;
    }
}

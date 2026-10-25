using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Rtx5090Monitoring.App.Controls;
using Rtx5090Monitoring.App.Services;
using Rtx5090Monitoring.App.Views;
using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Debugging.Models;
using Rtx5090Monitoring.Presentation.Settings;
using Rtx5090Monitoring.Presentation.Telemetry;
using Windows.Foundation;

namespace Rtx5090Monitoring.App.Diagnostics;

internal static class WireViewPresentationSmokeTests
{
    public static async Task RunAsync(MainWindow window, AppRuntime runtime, string output)
    {
        runtime.Settings.WarningNotifications = false;
        await PsuComparisonPreferencesSmokeTests.RunAsync(window, runtime, output);
        foreach (var theme in new[] { AppTheme.Dark, AppTheme.Light })
        {
            runtime.Settings.Theme = theme;
            var suffix = theme.ToString().ToLowerInvariant();
            window.Navigate("configuration");
            var configuration = (ConfigurationView)window.CurrentView!;
            configuration.SelectSection("plugin:wireview-pro2");
            await Task.Delay(300);
            var section = VisualTreeQuery.Descendants<SectionPanel>(configuration).Single();
            var enabled = VisualTreeQuery.Descendants<CheckBox>(section).Single(box => box.Content as string == "Enabled");
            var position = enabled.TransformToVisual(section).TransformPoint(new Point());
            if (position.X + enabled.ActualWidth < section.ActualWidth - 30 || VisualTreeQuery.Descendants<Expander>(section).Any())
            {
                throw new InvalidOperationException("Plugin enablement must be at the right of a non-collapsible section header.");
            }
            await SmokeTestRunner.CaptureElementAsync(window.ThemeRoot, Path.Combine(output, "wireview-settings-panel-" + suffix + ".png"));
            configuration.SelectSection("stream");
            await Task.Delay(150);
            if (!VisualTreeQuery.Descendants<SectionPanel>(configuration).Any(panel => panel.Header == "Sensor stream"))
            {
                throw new InvalidOperationException("Sensor stream must use the common header panel.");
            }
            configuration.SelectSection("sources");
            await Task.Delay(150);
            if (!VisualTreeQuery.Descendants<SectionPanel>(configuration).Any(panel => panel.Header == "Source selections"))
            {
                throw new InvalidOperationException("Source selections must use the common header panel.");
            }
            window.ShowPanel("wireview-pro2");
            await Task.Delay(300);
            var hardware = (HardwareView)window.CurrentView!;
            if (VisualTreeQuery.Descendants<Expander>(hardware).Any())
            {
                throw new InvalidOperationException("The inline device configuration expander must be removed.");
            }
            var button = VisualTreeQuery.Descendants<Button>(hardware).Single(item => item.Content as string == "Device settings...");
            ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
            await WaitAsync(() => runtime.WireViewSettingsWindow is { } device && VisualTreeQuery.Descendants<MetricTile>(device.ThemeRoot).Count() == 20);
            var deviceWindow = runtime.WireViewSettingsWindow!;
            await runtime.OpenWireViewDeviceSettingsAsync();
            if (!ReferenceEquals(deviceWindow, runtime.WireViewSettingsWindow))
            {
                throw new InvalidOperationException("Device settings must reuse the existing window.");
            }
            await Task.Delay(300);
            var scroll = VisualTreeQuery.Descendants<ScrollViewer>(deviceWindow.ThemeRoot).Single();
            if (scroll.ScrollableHeight <= 0 || scroll.ViewportHeight <= 0 || scroll.ScrollableWidth > 1)
            {
                throw new InvalidOperationException("The device settings list must be bounded and scroll vertically without horizontal overflow.");
            }
            var rows = VisualTreeQuery.Descendants<MetricTile>(deviceWindow.ThemeRoot).ToArray();
            var scrollbar = VisualTreeQuery.Descendants<ScrollBar>(scroll).Single(bar => bar.Orientation == Orientation.Vertical);
            var scrollbarLeft = scrollbar.TransformToVisual(scroll).TransformPoint(new Point()).X;
            if (rows.Any(row => VisualTreeQuery.Descendants<WidgetIcon>(row).Any()
                || row.TransformToVisual(scroll).TransformPoint(new Point(row.ActualWidth, 0)).X > scrollbarLeft - 2))
            {
                throw new InvalidOperationException("Device settings must have no icons and must leave a clear gutter before the scrollbar.");
            }
            await SmokeTestRunner.CaptureElementAsync(deviceWindow.ThemeRoot, Path.Combine(output, "wireview-device-window-" + suffix + ".png"));
            scroll.ChangeView(null, scroll.ScrollableHeight, null, true);
            await SmokeTestRunner.CaptureElementAsync(deviceWindow.ThemeRoot, Path.Combine(output, "wireview-device-window-bottom-" + suffix + ".png"));
            deviceWindow.Close();
            await WaitAsync(() => runtime.WireViewSettingsWindow is null);
        }

        runtime.OpenDebugConsole();
        var wire = runtime.Debug.Plugins.Single(plugin => plugin.Id == "wireview-pro2");
        wire.ModeIndex = (int)DebugSourceMode.Simulated;
        var fault = wire.Metrics.Single(metric => metric.Definition is { Component: SensorComponent.MonitorBoard, Metric: SensorMetric.FaultStatus });
        fault.ModeIndex = (int)DebugMetricMode.Fixed;
        fault.Value = 0x06;
        runtime.Debug.Enabled = true;
        window.ShowPanel("wireview-pro2");
        await WaitAsync(() => runtime.Dashboard.SelectedPanel is { HasFaults: true });
        foreach (var theme in new[] { AppTheme.Dark, AppTheme.Light })
        {
            runtime.Settings.Theme = theme;
            await Task.Delay(300);
            var hardware = (HardwareView)window.CurrentView!;
            var faults = VisualTreeQuery.Descendants<MetricTile>(hardware).Where(tile => tile.Model.IsWarning).ToArray();
            if (faults.Length != 2)
            {
                throw new InvalidOperationException("WireView must render one red warning row per reported fault.");
            }
            var headline = VisualTreeQuery.Descendants<Grid>(hardware).Single(grid => grid.Name == "HeadlineGrid");
            foreach (var tile in faults)
            {
                var icon = VisualTreeQuery.Descendants<WidgetIcon>(tile).Single();
                var surface = VisualTreeQuery.Descendants<Border>(icon).Single(border => border.Name == "IconSurface");
                var value = VisualTreeQuery.Descendants<TextBlock>(tile).Single(text => text.Name == "ValueText");
                var danger = ((SolidColorBrush)value.Foreground).Color;
                if (icon.Tone != StatusTone.Negative || ((SolidColorBrush)surface.Background).Color != danger
                    || danger.R <= danger.G
                    || tile.TransformToVisual(hardware).TransformPoint(new Point(0, tile.ActualHeight)).Y > headline.TransformToVisual(hardware).TransformPoint(new Point()).Y)
                {
                    throw new InvalidOperationException("Fault warnings and icon backgrounds must be red and appear above summary metrics.");
                }
            }
            await SmokeTestRunner.CaptureElementAsync(window.ThemeRoot, Path.Combine(output, "wireview-faults-" + theme.ToString().ToLowerInvariant() + ".png"));
        }
        fault.Value = 0;
        await WaitAsync(() => runtime.Dashboard.SelectedPanel is { HasFaults: false });
        runtime.DebugConsole!.Close();
        await WaitAsync(() => !runtime.Debug.Enabled && runtime.DebugConsole is null);
        if (runtime.Dashboard.Panels.Single(panel => panel.IsWireView).HasFaults)
        {
            throw new InvalidOperationException("Closing simulation must restore the actual fault state.");
        }
        runtime.OpenDebugConsole();
        if (runtime.Debug.Enabled)
        {
            throw new InvalidOperationException("Reopening the Debug Console must leave simulation disabled.");
        }
        runtime.DebugConsole!.Close();
        await File.WriteAllTextAsync(Path.Combine(output, "wireview-ui-results.txt"),
            "PASS: Non-collapsible header panels, right-aligned enable control, reusable icon-free WireView window, reserved scrollbar gutter, MSI-only comparison preferences, both themes, individual faults above metrics with red icons, recovery, and live restoration on Debug Console close/reopen.");
    }

    private static async Task WaitAsync(Func<bool> ready)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!ready())
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("WireView presentation did not reach the expected state.");
            }
            await Task.Delay(50);
        }
    }
}

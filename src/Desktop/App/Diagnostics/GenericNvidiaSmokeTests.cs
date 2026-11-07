using Microsoft.UI.Xaml.Controls;
using Rtx5090Monitoring.App.Services;
using Rtx5090Monitoring.App.Views;
using Rtx5090Monitoring.Debugging.Models;

namespace Rtx5090Monitoring.App.Diagnostics;

internal static class GenericNvidiaSmokeTests
{
    public static async Task RunAsync(MainWindow window, AppRuntime runtime, string output)
    {
        runtime.Settings.SetGpuStatusTabRequired(false);
        runtime.Settings.ShowSeparateGpuStatusTab = true;
        await Task.Delay(300);
        var shared = runtime.Dashboard.GpuStatus;

        foreach (var pluginId in new[] { "generic-nvidia", "asus-astral", "wireview-pro2" })
        {
            window.ShowPanel(pluginId);
            await Task.Delay(200);
            var view = VisualTreeQuery.Descendants<GpuStatusView>(window.ThemeRoot).Single();
            if (!ReferenceEquals(view.Model, shared) || view.Model.Metrics.Count != 5 || view.Model.IsUnavailable)
            {
                throw new InvalidOperationException("Every hardware tab must consume the same available GPU status model.");
            }

            await SmokeTestRunner.CaptureElementAsync(window.ThemeRoot, Path.Combine(output, pluginId + ".png"));
        }

        window.Navigate("configuration");
        var configuration = (ConfigurationView)window.CurrentView!;
        configuration.SelectSection("plugin:generic-nvidia");
        await Task.Delay(200);
        await runtime.RefreshNvidiaDevicesAsync();
        await Task.Delay(100);
        var selector = VisualTreeQuery.Descendants<ComboBox>(configuration).Single(box => box.Header as string == "Selected GPU"
            && box.ItemsSource is System.Collections.ObjectModel.ObservableCollection<Rtx5090Monitoring.Presentation.Configuration.GpuChoice> items
            && items.Any(item => item.Id == "demo-gpu0"));
        if (selector.SelectedItem is null)
        {
            throw new InvalidOperationException("Refreshing the NVIDIA selector must preserve a valid selection.");
        }

        await SmokeTestRunner.CaptureElementAsync(window.ThemeRoot, Path.Combine(output, "generic-nvidia-settings.png"));
        configuration.SelectSection("sources");
        await Task.Delay(200);
        var profileSelector = VisualTreeQuery.Descendants<ComboBox>(configuration)
            .Single(box => box.Header as string == "Active GPU status profile");
        if (profileSelector.Items.Count != 2 || VisualTreeQuery.Descendants<ConnectorMetricEditorView>(configuration).Count() != 3)
        {
            throw new InvalidOperationException("Source profiles must show two choices and three independent metric editors.");
        }

        profileSelector.SelectedIndex = 1;
        await Task.Delay(150);
        if (runtime.Configuration.ConnectorProfiles.SelectedProfile?.Name != "WireView"
            || VisualTreeQuery.Descendants<ConnectorMetricEditorView>(configuration).Any(view => view.Model.AllowFallback))
        {
            throw new InvalidOperationException("Selecting WireView must render its own fallback-disabled metric editors.");
        }

        await runtime.Configuration.ApplyAsync(CancellationToken.None);
        await Task.Delay(200);
        await SmokeTestRunner.CaptureElementAsync(window.ThemeRoot, Path.Combine(output, "connector-profiles.png"));
        runtime.OpenDebugConsole();
        foreach (var plugin in runtime.Debug.Plugins)
        {
            plugin.ModeIndex = (int)DebugSourceMode.Simulated;
        }

        runtime.Debug.Enabled = true;
        window.ShowPanel("generic-nvidia");
        await Task.Delay(200);
        if (runtime.Dashboard.GpuStatus.Metrics[3].Label != "Core / VRAM / HS")
        {
            throw new InvalidOperationException("A reported hotspot must join the existing temperature row.");
        }

        runtime.DebugConsole!.Close();
        await Task.Delay(250);
        if (runtime.Debug.Enabled)
        {
            throw new InvalidOperationException("Closing the debug console must restore live telemetry.");
        }

        await File.WriteAllTextAsync(Path.Combine(output, "gpu-status-result.txt"),
            "Shared GPU status, GPU selector refresh, hotspot simulation and close-to-live recovery passed.");
    }
}

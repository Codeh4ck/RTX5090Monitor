using Rtx5090Monitoring.Plugins.GenericNvidia.Contracts.Warnings;
using Microsoft.UI.Xaml.Controls;
using Rtx5090Monitoring.App.Services;
using Rtx5090Monitoring.App.Views;
using Rtx5090Monitoring.App.Views.Dialogs;
using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Contracts.Warnings;
using Rtx5090Monitoring.Debugging.Models;
using Rtx5090Monitoring.Presentation.Settings;

namespace Rtx5090Monitoring.App.Diagnostics;

internal static class WarningPolicySmokeTests
{
    public static async Task RunAsync(MainWindow window, AppRuntime runtime, string output)
    {
        runtime.Settings.WarningNotifications = false;
        window.Navigate("configuration");
        var view = (ConfigurationView)window.CurrentView!;
        view.SelectSection("warnings");
        await Task.Delay(300);
        var model = runtime.Configuration.Warnings;
        if (model.Defaults.RepeatIntervalSeconds != 5 || model.Defaults.RepeatWhileActive)
        {
            throw new InvalidOperationException("Reminders must default to five seconds with repetition off.");
        }
        var policy = VisualTreeQuery.Descendants<WarningPolicyView>(view).First();
        VisualTreeQuery.Descendants<CheckBox>(policy).Single(box => box.Content as string == "Repeat while warning remains active").IsChecked = true;
        runtime.Configuration.Plugins.Single(plugin => plugin.Id == "generic-nvidia").Nvidia.CoreTemperatureWarningC = 80;
        if (!runtime.Configuration.IsDirty || !view.IsFooterVisible)
        {
            throw new InvalidOperationException("Native policy edits must participate in the configuration draft.");
        }
        await runtime.Configuration.ApplyAsync(runtime.StopToken);
        if (runtime.Configuration.IsDirty || !runtime.Configuration.Warnings.Defaults.RepeatWhileActive)
        {
            throw new InvalidOperationException("Apply must retain the acknowledged reminder policy.");
        }
        var selector = VisualTreeQuery.Descendants<ComboBox>(view).Single(box => box.Header as string == "Warning type");
        selector.SelectedItem = model.Overrides.Single(item => item.TypeId == GenericNvidiaWarningIds.CoreTemperature);
        await Task.Delay(100);
        var special = model.Overrides.Single(item => item.TypeId == GenericNvidiaWarningIds.CoreTemperature);
        var custom = VisualTreeQuery.Descendants<CheckBox>(view).Single(box => box.Content as string == "Use global defaults");
        custom.IsChecked = false;
        special.Policy.NotifyOnStart = false;
        if (!runtime.Configuration.IsDirty)
        {
            throw new InvalidOperationException("A per-warning override must be tracked.");
        }

        custom.IsChecked = true;
        if (runtime.Configuration.IsDirty)
        {
            throw new InvalidOperationException("Removing the override must restore the clean configuration.");
        }

        foreach (var theme in new[] { AppTheme.Dark, AppTheme.Light })
        {
            runtime.Settings.Theme = theme;
            await SmokeTestRunner.CaptureElementAsync(window.ThemeRoot, Path.Combine(output, "warning-policy-" + theme.ToString().ToLowerInvariant() + ".png"));
            var dialog = new WarningNotificationsHelpDialog();
            var pending = view.ShowHelpAsync(dialog);
            await Task.Delay(250);
            if (!dialog.IsLoaded)
            {
                throw new InvalidOperationException("The warning help dialog did not open.");
            }

            await SmokeTestRunner.CaptureElementAsync(dialog, Path.Combine(output, "warning-help-" + theme.ToString().ToLowerInvariant() + ".png"));
            dialog.Hide();
            await pending;
        }
        runtime.OpenDebugConsole();
        foreach (var plugin in runtime.Debug.Plugins)
        {
            plugin.ModeIndex = (int)DebugSourceMode.Simulated;
        }

        runtime.Debug.Enabled = true;
        var temperature = runtime.Debug.Plugins.Single(plugin => plugin.Id == "generic-nvidia").Metrics.Single(metric => metric.Definition is
            { Component: SensorComponent.GpuCore, Metric: SensorMetric.Temperature });
        runtime.Settings.WarningNotifications = true;
        var baseline = runtime.DebugNotificationCount;
        temperature.ModeIndex = (int)DebugMetricMode.Fixed;
        temperature.Value = 81;
        await WaitAsync(() => runtime.DebugNotificationCount > baseline);
        var first = runtime.DebugNotificationCount;
        await Task.Delay(1000);
        temperature.Value = 82;
        if (runtime.DebugNotificationCount != first)
        {
            throw new InvalidOperationException("A changed reading must not produce an extra notification.");
        }

        runtime.SetRendering(false);
        await WaitAsync(() => runtime.DebugNotificationCount > first);
        var repeated = runtime.DebugNotificationCount;
        temperature.Value = 79;
        await Task.Delay(3200);
        temperature.Value = 83;
        await WaitAsync(() => runtime.DebugNotificationCount > repeated);
        runtime.DebugConsole!.Close();
        runtime.SetRendering(true);
        if (runtime.Debug.Enabled)
        {
            throw new InvalidOperationException("Closing the console must restore live warning data.");
        }

        await File.WriteAllTextAsync(Path.Combine(output, "warning-policy-results.txt"),
            "PASS: Five-second defaults, native edit/apply/revert, overrides, help in both themes, latest-value reminders, hidden monitoring, recovery rearming and live restoration.");
    }
    private static async Task WaitAsync(Func<bool> ready)
    {
        var deadline = DateTime.UtcNow.AddSeconds(9);
        while (!ready())
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("Warning policy did not reach the expected state.");
            }

            await Task.Delay(50);
        }
    }
}

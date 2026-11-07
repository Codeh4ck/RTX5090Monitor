using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Rtx5090Monitoring.App.Controls;
using Rtx5090Monitoring.App.Services;
using Rtx5090Monitoring.Client.Serialization;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Configuration;
using Rtx5090Monitoring.Presentation.Settings;

namespace Rtx5090Monitoring.App.Diagnostics;

internal static class MsiFanCurveSmokeTests
{
    public static async Task RunAsync(AppRuntime runtime, string output)
    {
        var api = runtime.SettingsProfileApi;
        var configuration = await api.GetConfigurationAsync(default);
        var plugins = new Dictionary<string, PluginSettings>(configuration.Configuration.Plugins)
        {
            ["msi-psu"] = configuration.Configuration.Plugins["msi-psu"] with { Enabled = true }
        };
        var enabled = await api.SetConfigurationAsync(configuration.Configuration with { Plugins = plugins }, configuration.Version!, default);
        runtime.Configuration.Receive(enabled);
        await runtime.OpenMsiFanCurveAsync();

        if (runtime.FanCurveWindow is not null)
        {
            throw new InvalidOperationException("The editor must not open unless Fan curve is selected on the main page.");
        }

        runtime.Configuration.Plugins.Single(plugin => plugin.IsMsi).Msi.FanModeIndex = (int)MsiFanControl.Curve;
        await runtime.OpenMsiFanCurveAsync();
        await Task.Delay(250);
        var window = runtime.FanCurveWindow ?? throw new InvalidOperationException("The curve editor did not open.");
        await runtime.OpenMsiFanCurveAsync();

        if (!ReferenceEquals(window, runtime.FanCurveWindow)
            || !VisualTreeQuery.Descendants<MsiFanCurveGraph>(window.ThemeRoot).Any()
            || VisualTreeQuery.Descendants<ComboBox>(window.ThemeRoot).Any())
        {
            throw new InvalidOperationException("The separate editor must reuse its graph window without a duplicate mode selector.");
        }

        window.Model.CurvePoints[0].TemperatureC = 35;
        window.Model.CurvePoints[0].DutyPercent = 50;
        window.Model.CurvePoints[1].TemperatureC = 40;
        window.Model.CurvePoints[1].DutyPercent = 30;
        window.Model.AddCurvePoint(70, 30);
        Invoke(window.ThemeRoot, "Apply");
        await WaitAsync(async () =>
        {
            var latest = await api.GetConfigurationAsync(default);
            var options = latest.Configuration.Plugins["msi-psu"].Options!.Value.Deserialize(ClientJsonContext.Default.MsiOptions)!;
            return options.FanControl == MsiFanControl.Curve && options.FanCurve.Points.Length == 3;
        });

        await Task.Delay(1200);

        // An HTTP failure must stay inside the editor, including its async timer callback.
        runtime.SetDemoDeviceAvailable("msi-psu", false);
        await WaitAsync(() => Task.FromResult(
            VisualTreeQuery.Descendants<TextBlock>(window.ThemeRoot).Any(text =>
                text.Name == "LiveStatus" && text.Visibility == Visibility.Visible
                && text.Text.Contains("No current MSI PSU snapshot is available."))));

        if (!ReferenceEquals(window, runtime.FanCurveWindow)
            || VisualTreeQuery.Descendants<TextBlock>(window.ThemeRoot)
                .Where(text => text.Name is "TemperatureValue" or "FanRpmValue" or "RequestedDutyValue")
                .Any(text => text.Text != "-"))
        {
            throw new InvalidOperationException("A 503 telemetry response must keep the editor open and clear stale live values.");
        }

        await SmokeTestRunner.CaptureElementAsync(window.ThemeRoot, Path.Combine(output, "msi-fan-curve-unavailable.png"));
        runtime.SetDemoDeviceAvailable("msi-psu", true);
        await WaitAsync(() => Task.FromResult(
            VisualTreeQuery.Descendants<TextBlock>(window.ThemeRoot).Any(text =>
                text.Name == "LiveStatus" && text.Visibility == Visibility.Collapsed)));

        // Saving an outdated draft must show the API conflict rather than close the app.
        var beforeConflict = await api.GetConfigurationAsync(default);
        await api.SetConfigurationAsync(beforeConflict.Configuration, beforeConflict.Version!, default);
        Invoke(window.ThemeRoot, "Apply");
        await WaitAsync(() => Task.FromResult(
            VisualTreeQuery.Descendants<InfoBar>(window.ThemeRoot).Any(bar =>
                bar.Name == "EditorError" && bar.IsOpen && bar.Message.Contains("Demo configuration changed"))));
        Invoke(window.ThemeRoot, "Revert / reload");
        await WaitAsync(() => Task.FromResult(
            VisualTreeQuery.Descendants<InfoBar>(window.ThemeRoot).Any(bar =>
                bar.Name == "EditorError" && !bar.IsOpen)));

        await WaitAsync(() => Task.FromResult(
            VisualTreeQuery.Descendants<NumberBox>(window.ThemeRoot).Any(input => input.Header as string == "Temp (°C)")));

        var temperature = VisualTreeQuery.Descendants<NumberBox>(window.ThemeRoot).First(input => input.Header as string == "Temp (°C)");
        temperature.Value = 35.4;
        var duty = VisualTreeQuery.Descendants<NumberBox>(window.ThemeRoot).First(input => input.Header as string == "Duty (%)");
        duty.Value = 50.6;

        if (temperature.Value != 35 || duty.Value != 51)
        {
            throw new InvalidOperationException("Curve fields must snap fractional input to whole integers.");
        }

        while (window.Model.CanAddCurvePoint)
        {
            window.Model.AddCurvePoint();
        }

        await Task.Delay(80);
        var addButton = VisualTreeQuery.Descendants<Button>(window.ThemeRoot).Single(button => button.Content as string == "Add point");
        window.Model.AddCurvePoint(99, 50);

        if (window.Model.CurvePoints.Count != 6 || addButton.IsEnabled)
        {
            throw new InvalidOperationException("The editor must stop and disable Add point at six points.");
        }

        foreach (var theme in new[] { AppTheme.Dark, AppTheme.Light })
        {
            runtime.Settings.Theme = theme;
            await Task.Delay(150);
            await SmokeTestRunner.CaptureElementAsync(window.ThemeRoot, Path.Combine(output, "msi-fan-curve-" + theme + ".png"));
        }

        window.Model.SetCurve(new MsiFanCurve { Points = [new(30, 40), new(60, 70)] });
        runtime.Settings.Theme = AppTheme.Dark;
        await Task.Delay(150);
        var graph = VisualTreeQuery.Descendants<MsiFanCurveGraph>(window.ThemeRoot).Single();
        graph.BeginDrag(window.Model.CurvePoints[^1]);
        var readout = VisualTreeQuery.Descendants<Border>(graph).Single(border => border.Name == "DragPointReadout");
        var readoutText = VisualTreeQuery.Descendants<TextBlock>(readout).Single();
        var x = 55 + 0.6 * (graph.ActualWidth - 80);
        graph.MoveDrag(new Windows.Foundation.Point(x, 20 + 0.35 * (graph.ActualHeight - 85)));

        if (readout.Visibility != Visibility.Visible || !readoutText.Text.Contains("60 °C") || !readoutText.Text.Contains("65%"))
        {
            throw new InvalidOperationException("Dragging must show the current snapped temperature and duty.");
        }

        graph.MoveDrag(new Windows.Foundation.Point(x, 20 + 0.3 * (graph.ActualHeight - 85)));

        if (!readoutText.Text.Contains("70%")
            || !ReferenceEquals(readout, VisualTreeQuery.Descendants<Border>(graph).Single(border => border.Name == "DragPointReadout")))
        {
            throw new InvalidOperationException("The drag readout must update in place.");
        }

        await SmokeTestRunner.CaptureElementAsync(window.ThemeRoot, Path.Combine(output, "msi-fan-curve-drag.png"));
        graph.EndDrag();

        if (readout.Visibility != Visibility.Collapsed)
        {
            throw new InvalidOperationException("The drag readout must close when dragging ends.");
        }

        var lowDuty = VisualTreeQuery.Descendants<NumberBox>(window.ThemeRoot).First(input => input.Header as string == "Duty (%)");
        lowDuty.Value = 17;

        if (lowDuty.Value != 0 || window.Model.CurvePoints[0].DutyPercent != 0)
        {
            throw new InvalidOperationException("Duty below 25% must snap to 0%.");
        }

        window.Model.SetCurve(new MsiFanCurve { Points = [new(30, 0), new(60, 0)] });
        Invoke(window.ThemeRoot, "Apply");
        await WaitAsync(async () =>
        {
            var latest = await api.GetConfigurationAsync(default);
            return latest.Configuration.Plugins["msi-psu"].Options!.Value.Deserialize(ClientJsonContext.Default.MsiOptions)!
                .FanCurve.Points.All(point => point.DutyPercent == 0);
        });
        var automatic = await api.GetMsiSnapshotAsync(default);

        if (!automatic.AutomaticFan || automatic.TargetFanDutyPercent != 0)
        {
            throw new InvalidOperationException("A zero curve request must hand cooling to PSU Automatic.");
        }

        await Task.Delay(1100);
        var values = VisualTreeQuery.Descendants<TextBlock>(window.ThemeRoot).ToArray();

        if (!values.Any(text => text.Text == "Temp.")
            || !values.Any(text => text.Name == "TemperatureValue" && text.Text.EndsWith("°C"))
            || !values.Any(text => text.Name == "FanRpmValue" && text.Text.Contains("(0%)"))
            || !values.Any(text => text.Name == "RequestedDutyValue" && text.Text == "0%")
            || VisualTreeQuery.Descendants<Border>(window.ThemeRoot).Count(border =>
                border.Name is "TemperatureBadge" or "FanRpmBadge" or "RequestedDutyBadge") != 3)
        {
            throw new InvalidOperationException("Live temperature, fan RPM/duty and requested duty must appear in separate status badges.");
        }

        await SmokeTestRunner.CaptureElementAsync(window.ThemeRoot, Path.Combine(output, "msi-fan-curve-badges.png"));

        var beforeReset = await api.GetConfigurationAsync(default);
        window.Model.CurvePoints[0].TemperatureC = double.NaN;
        Invoke(window.ThemeRoot, "Restore defaults");
        await Task.Delay(80);
        var afterReset = await api.GetConfigurationAsync(default);

        if (runtime.FanCurveWindow is not null || afterReset.Version != beforeReset.Version
            || runtime.Configuration.Plugins.Single(plugin => plugin.IsMsi).Msi.FanModeIndex != (int)MsiFanControl.Automatic)
        {
            throw new InvalidOperationException("Restore defaults must close the editor and select Automatic on the main page without writing the backend.");
        }

        await runtime.Configuration.ApplyAsync(default);
        var saved = await api.GetConfigurationAsync(default);
        var savedOptions = saved.Configuration.Plugins["msi-psu"].Options!.Value.Deserialize(ClientJsonContext.Default.MsiOptions)!;

        if (savedOptions.FanControl != MsiFanControl.Automatic || runtime.Configuration.IsDirty)
        {
            throw new InvalidOperationException("Main-page Apply must persist the restored mode using the current configuration version.");
        }

        await File.WriteAllTextAsync(Path.Combine(output, "msi-fan-curve-pass.txt"),
            "PASS: Main mode gating, no duplicate selector, falling duty, whole-number input, six-point cap, restore-to-main draft, version synchronization, HTTP 503 telemetry recovery, configuration conflicts and dark/light rendering.");
    }

    private static void Invoke(FrameworkElement root, string caption)
    {
        var button = VisualTreeQuery.Descendants<Button>(root).Single(button => button.Content as string == caption);
        ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
    }

    private static async Task WaitAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(8);

        while (!await condition())
        {
            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException("The fan curve UI did not apply its draft.");
            }

            await Task.Delay(80);
        }
    }
}

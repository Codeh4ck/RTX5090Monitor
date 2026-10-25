using Microsoft.UI.Xaml.Controls;
using Rtx5090Monitoring.App.Controls;
using Rtx5090Monitoring.App.Services;
using Rtx5090Monitoring.App.Views;
using Rtx5090Monitoring.Presentation.Settings;

namespace Rtx5090Monitoring.App.Diagnostics;

internal static class PsuComparisonPreferencesSmokeTests
{
    public static async Task RunAsync(MainWindow window, AppRuntime runtime, string output)
    {
        var original = runtime.Settings.Value;
        window.Navigate("settings");
        await Task.Delay(200);
        if (VisualTreeQuery.Descendants<SectionPanel>(window.ThemeRoot).Any(panel => panel.Header == "PSU pin comparison"))
        {
            throw new InvalidOperationException("General settings must not render MSI comparison preferences.");
        }

        window.Navigate("configuration");
        var view = (ConfigurationView)window.CurrentView!;
        foreach (var plugin in new[] { "wireview-pro2", "asus-astral" })
        {
            view.SelectSection("plugin:" + plugin);
            await Task.Delay(150);
            if (VisualTreeQuery.Descendants<SectionPanel>(view).Any(panel => panel.Header == "PSU pin comparison"))
            {
                throw new InvalidOperationException("Comparison preferences must only render on the MSI plugin tab.");
            }
        }

        view.SelectSection("plugin:msi-psu");
        await Task.Delay(200);
        var comparison = VisualTreeQuery.Descendants<SectionPanel>(view).Single(panel => panel.Header == "PSU pin comparison");
        var toggles = VisualTreeQuery.Descendants<ToggleSwitch>(comparison).ToArray();
        var wire = toggles.Single(toggle => toggle.Header as string == "Show MSI current under WireView pins");
        var asus = toggles.Single(toggle => toggle.Header as string == "Show MSI current under ROG Astral pins");
        var selectors = VisualTreeQuery.Descendants<ComboBox>(comparison).ToArray();
        var wireConnector = selectors.Single(selector => selector.Header as string == "WireView · PSU connector");
        var asusConnector = selectors.Single(selector => selector.Header as string == "ROG Astral · PSU connector");
        try
        {
            wire.IsOn = false;
            asus.IsOn = false;
            await Task.Delay(100);
            if (wireConnector.IsEnabled || asusConnector.IsEnabled)
            {
                throw new InvalidOperationException("Each connector selector must follow its own comparison toggle.");
            }
            wire.IsOn = true;
            asus.IsOn = true;
            wireConnector.SelectedIndex = 1;
            asusConnector.SelectedIndex = 0;
            await Task.Delay(150);
            if (runtime.Settings.Value is not { ShowWireViewPsuCurrent: true, ShowAsusPsuCurrent: true, WireViewPsuConnector: 2, AsusPsuConnector: 1 }
                || runtime.Configuration.IsDirty)
            {
                throw new InvalidOperationException("MSI comparison controls must edit existing local display preferences without dirtying backend configuration.");
            }
            view.SelectSection("plugin:wireview-pro2");
            view.SelectSection("plugin:msi-psu");
            if (wireConnector.SelectedIndex != 1 || asusConnector.SelectedIndex != 0)
            {
                throw new InvalidOperationException("Tab switches must retain both connector selections.");
            }
            foreach (var theme in new[] { AppTheme.Dark, AppTheme.Light })
            {
                runtime.Settings.Theme = theme;
                await SmokeTestRunner.CaptureElementAsync(window.ThemeRoot, Path.Combine(output, "msi-comparison-settings-" + theme.ToString().ToLowerInvariant() + ".png"));
            }
        }
        finally
        {
            runtime.Settings.ShowWireViewPsuCurrent = original.ShowWireViewPsuCurrent;
            runtime.Settings.ShowAsusPsuCurrent = original.ShowAsusPsuCurrent;
            runtime.Settings.WireViewPsuConnectorIndex = original.WireViewPsuConnector - 1;
            runtime.Settings.AsusPsuConnectorIndex = original.AsusPsuConnector - 1;
        }
    }
}

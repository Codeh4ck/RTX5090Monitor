using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rtx5090Monitoring.App.Services;
using Rtx5090Monitoring.Presentation.Configuration;
using Rtx5090Monitoring.Presentation.Settings;

namespace Rtx5090Monitoring.App.Views;

public sealed partial class PluginSettingsView : UserControl
{
    private readonly AppRuntime _runtime;
    public PluginSettingsEditorViewModel Model { get; }
    public AppSettingsViewModel Preferences { get; }

    internal PluginSettingsView(PluginSettingsEditorViewModel model, AppRuntime runtime)
    {
        Model = model;
        _runtime = runtime;
        Preferences = runtime.Settings;
        InitializeComponent();
        OvercurrentInput.NumberFormatter = new Windows.Globalization.NumberFormatting.DecimalFormatter
        {
            FractionDigits = 0,
            NumberRounder = new Windows.Globalization.NumberFormatting.IncrementNumberRounder { Increment = 0.0001 }
        };
    }

    private async void RefreshDevices_Click(object sender, RoutedEventArgs args) => await _runtime.RefreshDevicesAsync();
    private async void RefreshNvidiaDevices_Click(object sender, RoutedEventArgs args) => await _runtime.RefreshNvidiaDevicesAsync();

    private async void RefreshMsiDevices_Click(object sender, RoutedEventArgs args) => await _runtime.RefreshMsiDevicesAsync();
}

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
    }

    private async void FanCurve_Click(object sender, RoutedEventArgs args) => await _runtime.OpenMsiFanCurveAsync();

    private async void RefreshDevices_Click(object sender, RoutedEventArgs args) => await _runtime.RefreshDevicesAsync();
    private async void RefreshNvidiaDevices_Click(object sender, RoutedEventArgs args) => await _runtime.RefreshNvidiaDevicesAsync();

    private async void RefreshMsiDevices_Click(object sender, RoutedEventArgs args) => await _runtime.RefreshMsiDevicesAsync();
}

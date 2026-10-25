using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rtx5090Monitoring.App.Services;
using Rtx5090Monitoring.Presentation.Settings;

namespace Rtx5090Monitoring.App.Views;

public sealed partial class SettingsView : UserControl
{
    private readonly AppRuntime _runtime;
    public AppSettingsViewModel Model { get; }

    internal SettingsView(AppRuntime runtime)
    {
        _runtime = runtime;
        Model = runtime.Settings;
        InitializeComponent();
        ThemePicker.SelectedIndex = Model.Theme switch { AppTheme.Dark => 1, AppTheme.Light => 2, _ => 0 };
        StartupToggle.IsEnabled = !runtime.IsDemo;
    }

    private void Theme_Changed(object sender, SelectionChangedEventArgs args)
    {
        if (ThemePicker.SelectedIndex >= 0)
        {
            Model.Theme = ThemePicker.SelectedIndex switch { 1 => AppTheme.Dark, 2 => AppTheme.Light, _ => AppTheme.System };
        }
    }

    private void Reconnect_Click(object sender, RoutedEventArgs args) => _runtime.Reconnect();
}

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rtx5090Monitoring.App.Debugging.Views;
using Rtx5090Monitoring.App.Services;
using Rtx5090Monitoring.Debugging;
using Rtx5090Monitoring.Presentation.Settings;

namespace Rtx5090Monitoring.App.Debugging.Windows;

internal sealed partial class DebugWindow : Window
{
    public DebugSession Model { get; }
    internal FrameworkElement ThemeRoot => Root;
    internal DebugWindow(AppRuntime runtime)
    {
        Model = runtime.Debug;
        InitializeComponent();
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "monitor.ico"));
        foreach (var plugin in Model.Plugins)
        {
            PluginTabs.TabItems.Add(new TabViewItem
            {
                Header = plugin.Name, Tag = plugin.Id, IsClosable = false, Style = (Style)Root.Resources["MonitorConfigurationTab"],
                Content = new DebugPluginView(plugin, Model.ResetPlugin)
            });
        }
        ApplyAppearance(runtime.Preferences);
        Closed += (_, _) =>
        {
            Model.Enabled = false;
            Bindings.StopTracking();
        };
    }
    internal void ApplyAppearance(AppPreferences preferences) => ThemeService.Apply(Root, preferences);
    internal void SelectPlugin(string id) => PluginTabs.SelectedItem = PluginTabs.TabItems.OfType<TabViewItem>().Single(tab => tab.Tag as string == id);
    private void Disable_Click(object sender, RoutedEventArgs args) => Model.Enabled = false;
    private void ResetAll_Click(object sender, RoutedEventArgs args) => Model.ResetAll();
}

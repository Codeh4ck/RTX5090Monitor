using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rtx5090Monitoring.App.Views.Dialogs;
using Rtx5090Monitoring.App.Services;
using Rtx5090Monitoring.Presentation.Telemetry;
using Rtx5090Monitoring.Presentation.Configuration;

namespace Rtx5090Monitoring.App.Views;

public sealed partial class WarningOptionsView : UserControl
{
    private bool _helpOpen;
    public ConfigurationEditorViewModel Editor { get; }
    public WarningOptionsEditorViewModel Model => Editor.Warnings;
    public DashboardViewModel Dashboard { get; }
    internal WarningOptionsView(ConfigurationEditorViewModel editor, AppRuntime runtime)
    {
        Editor = editor;
        Dashboard = runtime.Dashboard;
        InitializeComponent();
        DefaultsHost.Content = new WarningPolicyView(Model.Defaults);
        WarningPicker.SelectedIndex = 0;
    }
    private void Warning_Changed(object sender, SelectionChangedEventArgs args)
    {
        OverrideHost.Content = WarningPicker.SelectedItem is WarningOverrideEditorViewModel model ? new WarningOverrideView(model) : null;
    }
    private async void Info_Click(object sender, RoutedEventArgs args)
    {
        if (_helpOpen)
        {
            return;
        }

        _helpOpen = true;
        try
        {
            await new WarningNotificationsHelpDialog { XamlRoot = XamlRoot, RequestedTheme = ActualTheme }.ShowAsync();
        }
        finally { _helpOpen = false; }
    }
}

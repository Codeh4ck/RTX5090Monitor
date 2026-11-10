using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RTXLens.App.Views.Dialogs;
using RTXLens.App.Services;
using RTXLens.Presentation.Telemetry;
using RTXLens.Presentation.Configuration;

namespace RTXLens.App.Views;

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
    private void Warning_Changed(object sender, SelectionChangedEventArgs args) => OverrideHost.Content = WarningPicker.SelectedItem is WarningOverrideEditorViewModel model ? new WarningOverrideView(model) : null;

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

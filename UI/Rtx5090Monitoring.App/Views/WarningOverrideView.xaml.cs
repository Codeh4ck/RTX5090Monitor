using Microsoft.UI.Xaml.Controls;
using Rtx5090Monitoring.Presentation.Configuration;

namespace Rtx5090Monitoring.App.Views;

public sealed partial class WarningOverrideView : UserControl
{
    public WarningOverrideEditorViewModel Model { get; }
    public WarningOverrideView(WarningOverrideEditorViewModel model)
    {
        Model = model;
        InitializeComponent();
        PolicyHost.Content = new WarningPolicyView(Model.Policy);
    }
}

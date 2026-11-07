using Microsoft.UI.Xaml.Controls;
using Rtx5090Monitoring.Presentation.Configuration;

namespace Rtx5090Monitoring.App.Views;

public sealed partial class WarningPolicyView : UserControl
{
    public WarningPolicyEditorViewModel Model { get; }
    public WarningPolicyView(WarningPolicyEditorViewModel model)
    {
        Model = model;
        InitializeComponent();
    }
}

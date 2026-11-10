using Microsoft.UI.Xaml.Controls;
using RTXLens.Presentation.Configuration;

namespace RTXLens.App.Views;

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

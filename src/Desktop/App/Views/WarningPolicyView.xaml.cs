using Microsoft.UI.Xaml.Controls;
using RTXLens.Presentation.Configuration;

namespace RTXLens.App.Views;

public sealed partial class WarningPolicyView : UserControl
{
    public WarningPolicyEditorViewModel Model { get; }
    public WarningPolicyView(WarningPolicyEditorViewModel model)
    {
        Model = model;
        InitializeComponent();
    }
}

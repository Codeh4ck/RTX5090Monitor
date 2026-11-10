using Microsoft.UI.Xaml.Controls;

namespace RTXLens.App.Views.Dialogs;

public sealed partial class ExitConfirmationDialog : ContentDialog
{
    public ExitConfirmationDialog()
    {
        InitializeComponent();
    }

    internal bool SuppressFutureConfirmation => DoNotShowAgain.IsChecked == true;
}

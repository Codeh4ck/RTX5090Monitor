using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Rtx5090Monitoring.App.Views.Dialogs;
using Rtx5090Monitoring.Presentation.Settings;

namespace Rtx5090Monitoring.App.Services;

internal static class ExitConfirmation
{
    public static async Task<bool> ConfirmAsync(FrameworkElement root, AppSettingsViewModel settings)
    {
        if (!settings.ConfirmBeforeExit)
        {
            return true;
        }

        if (root.XamlRoot is not { } xamlRoot)
        {
            return false;
        }

        // An exit request cancels any existing dialog rather than confirming its action.
        foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(xamlRoot).ToArray())
        {
            if (FindDialog(popup.Child) is not { } existing)
            {
                continue;
            }

            var closed = new TaskCompletionSource();
            void OnClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
            {
                sender.Closed -= OnClosed;
                closed.TrySetResult();
            }

            existing.Closed += OnClosed;
            existing.Hide();
            await closed.Task;
        }

        var dialog = new ExitConfirmationDialog
        {
            XamlRoot = xamlRoot,
            RequestedTheme = root.ActualTheme
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return false;
        }

        if (dialog.SuppressFutureConfirmation)
        {
            settings.ConfirmBeforeExit = false;
        }

        return true;
    }

    private static ContentDialog? FindDialog(DependencyObject? element)
    {
        if (element is ContentDialog dialog)
        {
            return dialog;
        }

        if (element is null)
        {
            return null;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
        {
            if (FindDialog(VisualTreeHelper.GetChild(element, index)) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}

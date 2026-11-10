using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RTXLens.App.Services;
using RTXLens.App.Views.Dialogs;
using RTXLens.Presentation.Settings;

namespace RTXLens.App.Diagnostics;

internal static class ExitConfirmationSmokeTests
{
    public static async Task RunAsync(MainWindow window, AppRuntime runtime, string output)
    {
        runtime.Settings.Theme = AppTheme.Dark;
        runtime.Settings.ConfirmBeforeExit = true;

        var cancelled = ExitConfirmation.ConfirmAsync(window.ThemeRoot, runtime.Settings);
        var dialog = await FindDialogAsync(window);
        var checkbox = VisualTreeQuery.Descendants<CheckBox>(dialog).Single();
        checkbox.IsChecked = true;
        await SmokeTestRunner.CaptureElementAsync(dialog, Path.Combine(output, "exit-confirmation.png"));
        Invoke(dialog, "CloseButton");
        if (await cancelled || !runtime.Settings.ConfirmBeforeExit)
        {
            throw new InvalidOperationException("Cancel must keep the app running without saving the opt-out.");
        }

        var help = new BackendRestartHelpDialog { XamlRoot = window.ThemeRoot.XamlRoot };
        var helpTask = help.ShowAsync();
        await Task.Delay(200);

        var confirmed = ExitConfirmation.ConfirmAsync(window.ThemeRoot, runtime.Settings);
        dialog = await FindDialogAsync(window);
        await helpTask;
        checkbox = VisualTreeQuery.Descendants<CheckBox>(dialog).Single();
        checkbox.IsChecked = true;
        Invoke(dialog, "PrimaryButton");

        if (!await confirmed || runtime.Settings.ConfirmBeforeExit)
        {
            throw new InvalidOperationException("Confirm must accept exit and save the checked opt-out.");
        }

        if (!await ExitConfirmation.ConfirmAsync(window.ThemeRoot, runtime.Settings))
        {
            throw new InvalidOperationException("Opted-out exits must proceed without a dialog.");
        }

        runtime.Settings.ConfirmBeforeExit = true;
        await File.WriteAllTextAsync(Path.Combine(output, "verification.txt"),
            "PASS: Exit dialog renders; Cancel preserves confirmation; existing dialogs are cancelled safely; confirmed opt-out bypasses subsequent prompts.");
    }

    private static async Task<ExitConfirmationDialog> FindDialogAsync(MainWindow window)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var dialog = VisualTreeHelper.GetOpenPopupsForXamlRoot(window.ThemeRoot.XamlRoot)
                .SelectMany(popup => popup.Child is ExitConfirmationDialog dialog
                    ? new[] { dialog }
                    : VisualTreeQuery.Descendants<ExitConfirmationDialog>(popup.Child))
                .FirstOrDefault(candidate => candidate.IsLoaded);
            if (dialog is not null)
            {
                await Task.Delay(200);
                return dialog;
            }

            await Task.Delay(100);
        }

        throw new InvalidOperationException("Exit confirmation did not appear.");
    }

    private static void Invoke(ContentDialog dialog, string name)
    {
        var button = VisualTreeQuery.Descendants<Button>(dialog).Single(candidate => candidate.Name == name);
        ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
    }
}

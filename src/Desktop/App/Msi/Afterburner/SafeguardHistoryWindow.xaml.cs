using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Rtx5090Monitoring.App.Services;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Safeguard;
using Rtx5090Monitoring.Presentation.Settings;
using Rtx5090Monitoring.Presentation.Telemetry;

namespace Rtx5090Monitoring.App.Msi.Afterburner;

internal sealed partial class SafeguardHistoryWindow : Window
{
    private readonly AppRuntime _runtime;
    private SafeguardStorage? _storage;
    private SafeguardHistoryRow[] _rows = [];
    private bool _dialogOpen;

    public SafeguardHistoryWindow(AppRuntime runtime)
    {
        _runtime = runtime;
        InitializeComponent();
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "monitor.ico"));
        ApplyAppearance(runtime.Preferences);
        try
        {
            _storage = new SafeguardStorage();
            Retention.Value = _storage.RetentionDays;
            Refresh();
        }
        catch (Exception error) when (SafeguardScriptDialog.IsConfigurationError(error))
        {
            Message.Text = error.Message;
        }

        runtime.SafeguardHistoryChanged += Refresh;
        Closed += (_, _) => runtime.SafeguardHistoryChanged -= Refresh;
    }

    internal void ApplyAppearance(AppPreferences preferences) => ThemeService.Apply(Root, preferences);

    private void Refresh()
    {
        if (_storage is not { } storage)
        {
            return;
        }

        try
        {
            var now = DateTimeOffset.UtcNow;
            var cutoff = now.AddDays(-storage.RetentionDays);
            var rows = storage.Ids("history").Select(id => storage.Read<SafeguardExecution>("history", id))
                .OfType<SafeguardExecution>().Where(run => run.StartedAt >= cutoff)
                .OrderByDescending(run => run.StartedAt).ThenBy(run => run.Id)
                .Select(run => new SafeguardHistoryRow(run.Id, run.Name, run.StartedAt.LocalDateTime.ToString("g"),
                    SafeguardStorage.DisplayStatus(run, now), SafeguardStorage.IsRunning(run, now))).ToArray();
            if (!rows.SequenceEqual(_rows))
            {
                var selection = Selected().Select(row => row.Id).ToHashSet();
                _rows = rows;
                Executions.ItemsSource = rows;
                foreach (var row in rows.Where(row => selection.Contains(row.Id)))
                {
                    Executions.SelectedItems.Add(row);
                }
            }

            _runtime.SetSafeguardHistoryAvailability(rows.Length > 0);
            Message.Text = rows.Length == 0 ? "No executions are available within the retention period." : "";
        }
        catch (Exception error) when (SafeguardScriptDialog.IsConfigurationError(error))
        {
            Message.Text = error.Message;
        }
    }

    private SafeguardHistoryRow[] Selected() => Executions.SelectedItems.OfType<SafeguardHistoryRow>().ToArray();

    private void Selection_Changed(object sender, SelectionChangedEventArgs args)
    {
        var selected = Selected();
        InspectButton.IsEnabled = selected.Length == 1;
        DeleteButton.IsEnabled = selected.Length > 0 && selected.All(row => !row.Running);
    }

    private async void Inspect_Click(object sender, RoutedEventArgs args) => await InspectAsync();

    private async void Executions_DoubleTapped(object sender, DoubleTappedRoutedEventArgs args) => await InspectAsync();

    private async Task InspectAsync()
    {
        if (_dialogOpen || Selected() is not [var row] || _storage is not { } storage)
        {
            return;
        }

        _dialogOpen = true;
        try
        {
            if (storage.Read<SafeguardExecution>("history", row.Id) is not { } run)
            {
                Refresh();
                return;
            }

            var delivery = storage.Read<SafeguardDelivery>("delivery", run.Id);
            var binding = storage.Read<SafeguardBinding>("bindings", run.BindingId);
            var details = new List<string>
            {
                $"Execution ID: {run.Id:D}",
                $"Script version: {run.Version}",
                $"Connector: {run.Connector}",
                $"Started: {run.StartedAt.ToLocalTime():G}",
                $"Finished: {run.FinishedAt?.ToLocalTime().ToString("G") ?? "-"}",
                $"Status: {SafeguardStorage.DisplayStatus(run, DateTimeOffset.UtcNow)}",
                $"Shutdown delay: {run.DelayMs:N0} ms"
            };
            if (binding?.MailId is { } mailId && storage.Read<SafeguardMailCredential>("mail", mailId) is { } mail)
            {
                details.Add($"SMTP: {mail.Settings.Host}:{mail.Settings.Port} ({mail.Settings.Security})");
                details.Add($"Sender: {mail.Settings.Sender}");
                details.Add($"Recipients: {string.Join(", ", mail.Settings.Recipients)}");
                details.Add(mail.Settings.Authenticate ? "SMTP credentials: hidden" : "SMTP authentication: none");
                details.Add($"Email: {delivery?.Status ?? "Pending"}");
                if (delivery?.Detail is { } problem)
                {
                    details.Add(problem);
                }
            }

            details.Add("");
            details.AddRange(run.Steps.Select(step => $"{step.Timestamp.ToLocalTime():T} · {step.Name}\n{step.Result}"
                + (step.ExitCode is { } exitCode ? $" (exit code {exitCode})" : "")));
            await new ContentDialog
            {
                Title = run.Name, XamlRoot = Root.XamlRoot, CloseButtonText = "Close",
                Content = new ScrollViewer
                {
                    MaxHeight = 460,
                    Content = new TextBlock { Text = string.Join("\n", details), TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true }
                }
            }.ShowAsync();
        }
        catch (Exception error) when (SafeguardScriptDialog.IsConfigurationError(error))
        {
            Message.Text = error.Message;
        }
        finally
        {
            _dialogOpen = false;
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs args)
    {
        var selected = Selected();
        if (_dialogOpen || selected.Length == 0 || _storage is not { } storage)
        {
            return;
        }

        _dialogOpen = true;
        try
        {
            var confirmed = await new ContentDialog
            {
                XamlRoot = Root.XamlRoot, Title = $"Delete {selected.Length} execution record(s)?",
                Content = "The selected records will be permanently removed from Safeguard+ History.",
                PrimaryButtonText = "Delete", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close
            }.ShowAsync();
            if (confirmed == ContentDialogResult.Primary)
            {
                storage.DeleteExecutions(selected.Select(row => row.Id), DateTimeOffset.UtcNow);
                Refresh();
            }
        }
        catch (Exception error) when (SafeguardScriptDialog.IsConfigurationError(error))
        {
            Message.Text = error.Message;
        }
        finally
        {
            _dialogOpen = false;
        }
    }

    private void SaveRetention_Click(object sender, RoutedEventArgs args)
    {
        if (_storage is not { } storage)
        {
            return;
        }

        try
        {
            if (!AfterburnerNumericInput.Commit(Retention) || !double.IsFinite(Retention.Value) || Retention.Value % 1 != 0)
            {
                throw new ArgumentException("Enter a whole number of days.");
            }

            storage.RetentionDays = checked((int)Retention.Value);
            Refresh();
            Message.Text = "History retention saved.";
        }
        catch (Exception error) when (SafeguardScriptDialog.IsConfigurationError(error))
        {
            Message.Text = error.Message;
        }
    }
}
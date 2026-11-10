using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using RTXLens.App.Services;
using RTXLens.Plugins.Msi.Contracts.Safeguard;
using RTXLens.Presentation.Settings;
using RTXLens.Presentation.Telemetry;
using RTXLens.SafeguardReporting;

namespace RTXLens.App.Msi.Afterburner;

internal sealed partial class SafeguardHistoryWindow : Window
{
    private readonly AppRuntime _runtime;
    private SafeguardStorage? _storage;
    private SafeguardHistoryRow[] _rows = [];
    private bool _dialogOpen;
    private SafeguardExecutionWindow? _inspection;

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
        Closed += (_, _) =>
        {
            runtime.SafeguardHistoryChanged -= Refresh;
            _inspection?.Close();
        };
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

    private void Inspect_Click(object sender, RoutedEventArgs args) => Inspect();

    private void Executions_DoubleTapped(object sender, DoubleTappedRoutedEventArgs args) => Inspect();

    private void Inspect()
    {
        if (_dialogOpen || Selected() is not [var row] || _storage is not { } storage)
        {
            return;
        }

        if (_inspection is { } current && current.ExecutionId == row.Id)
        {
            current.Activate();
            return;
        }

        try
        {
            if (storage.Read<SafeguardExecution>("history", row.Id) is not { } run)
            {
                Refresh();
                return;
            }

            var telemetry = storage.Read<SafeguardTelemetry>("telemetry", run.Id);
            var binding = storage.Read<SafeguardBinding>("bindings", run.BindingId);
            var report = SafeguardReport.ForExecution(run, SafeguardStorage.DisplayStatus(run, DateTimeOffset.UtcNow), telemetry) with
            {
                Delivery = storage.Read<SafeguardDelivery>("delivery", run.Id),
                MailSettings = binding?.MailId is { } mailId ? storage.Read<SafeguardMailCredential>("mail", mailId)?.Settings : null
            };

            _inspection?.Close();
            var window = new SafeguardExecutionWindow(report, _runtime.Preferences, Root.XamlRoot.RasterizationScale);
            _inspection = window;
            window.Closed += (_, _) =>
            {
                if (_inspection == window)
                {
                    _inspection = null;
                }
            };
            window.Activate();
        }
        catch (Exception error) when (SafeguardScriptDialog.IsConfigurationError(error))
        {
            Message.Text = error.Message;
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
using System.Text.Json;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rtx5090Monitoring.App.Controls;
using Rtx5090Monitoring.App.Services;
using Rtx5090Monitoring.Client.Http;
using Rtx5090Monitoring.Client.Serialization;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Configuration;
using Rtx5090Monitoring.Presentation.Configuration;
using Rtx5090Monitoring.Presentation.Settings;

namespace Rtx5090Monitoring.App.Msi.Windows;

internal sealed partial class MsiFanCurveWindow : Window
{
    private readonly IMonitoringApi _api;
    private readonly Action<ConfigurationSnapshot, MsiFanCurve, string> _onSaved;
    private readonly Action _restoreDefaults;
    private readonly MsiFanCurveGraph _graph = new();
    private readonly DispatcherQueueTimer _timer;
    private readonly CancellationTokenSource _stop = new();
    private ConfigurationSnapshot? _baseline;
    private MsiSettingsEditorViewModel _model = new(new MsiOptions());
    private bool _busy;
    private bool _reading;
    private bool _closed;
    private bool? _stacked;
    private bool _readyToFitHeight;
    private bool _heightFitQueued;

    public MsiSettingsEditorViewModel Model => _model;
    internal FrameworkElement ThemeRoot => Root;

    internal MsiFanCurveWindow(IMonitoringApi api, AppPreferences preferences,
        Action<ConfigurationSnapshot, MsiFanCurve, string> onSaved, Action restoreDefaults)
    {
        _api = api;
        _onSaved = onSaved;
        _restoreDefaults = restoreDefaults;
        InitializeComponent();
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "monitor.ico"));
        GraphHost.Content = _graph;
        _graph.SetModel(Model);
        ApplyAppearance(preferences);

        _timer = Root.DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += OnTick;
        Closed += OnClosed;
        Root.SizeChanged += (_, _) => LayoutEditor();
        Root.Loaded += (_, _) => QueueHeightFit();
        HeaderPanel.SizeChanged += (_, _) => QueueHeightFit();
        EditorLayout.SizeChanged += (_, _) => QueueHeightFit();
        SetActionsEnabled(false);
    }

    internal void ApplyAppearance(AppPreferences preferences) => ThemeService.Apply(Root, preferences);

    internal async Task InitializeAsync()
    {
        await ReloadAsync();

        if (!_closed)
        {
            await ReadLiveAsync();
            _timer.Start();
            _readyToFitHeight = true;
            QueueHeightFit();
        }
    }

    private void QueueHeightFit()
    {
        if (_closed || !_readyToFitHeight || _heightFitQueued)
        {
            return;
        }

        _heightFitQueued = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            _heightFitQueued = false;
            FitContentHeight();
        });
    }

    private void FitContentHeight()
    {
        if (_closed || !Root.IsLoaded || Root.XamlRoot is not { } xamlRoot)
        {
            return;
        }

        Root.UpdateLayout();

        if (Root.ActualHeight <= 0 || EditorLayout.ActualHeight <= 0)
        {
            return;
        }

        var scale = xamlRoot.RasterizationScale;
        var frameHeight = AppWindow.Size.Height - (int)Math.Round(Root.ActualHeight * scale);
        var contentHeight = Root.Padding.Top + HeaderPanel.ActualHeight + Root.RowSpacing
            + EditorScroll.Padding.Top + EditorLayout.ActualHeight + EditorScroll.Padding.Bottom + Root.Padding.Bottom;
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var height = Math.Min(area.Height, (int)Math.Ceiling((contentHeight + 2) * scale) + frameHeight);
        var top = Math.Clamp(AppWindow.Position.Y, area.Y, area.Y + area.Height - height);

        if (AppWindow.Size.Height != height || AppWindow.Position.Y != top)
        {
            AppWindow.MoveAndResize(new global::Windows.Graphics.RectInt32(
                AppWindow.Position.X, top, AppWindow.Size.Width, height));
        }
    }

    private async void OnTick(DispatcherQueueTimer sender, object args) => await ReadLiveAsync();

    private async Task ReadLiveAsync()
    {
        if (_reading || _closed)
        {
            return;
        }

        _reading = true;

        try
        {
            var snapshot = await _api.GetMsiSnapshotAsync(_stop.Token);

            if (_closed)
            {
                return;
            }

            if (DateTimeOffset.UtcNow - snapshot.ObservedAt > TimeSpan.FromSeconds(5))
            {
                throw new InvalidDataException("The PSU fan readings are stale.");
            }

            _graph.UpdateLive(snapshot);
            TemperatureValue.Text = $"{snapshot.TemperatureC:0} °C";
            FanRpmValue.Text = $"{snapshot.FanRpm:0} ({snapshot.FanDutyPercent}%)";
            RequestedDutyValue.Text = snapshot.TargetFanDutyPercent is { } target ? target + "%" : "-";
            LiveStatus.Visibility = Visibility.Collapsed;
            ControllerWarning.Message = snapshot.FanControllerMessage ?? "";
            ControllerWarning.IsOpen = snapshot.FanControllerMessage is not null;
        }
        catch (Exception exception) when (IsRequestError(exception))
        {
            if (!_closed)
            {
                _graph.UpdateLive(null);
                TemperatureValue.Text = FanRpmValue.Text = RequestedDutyValue.Text = "-";
                LiveStatus.Text = "Live PSU readings unavailable - " + exception.Message;
                LiveStatus.Visibility = Visibility.Visible;
                ControllerWarning.IsOpen = false;
            }
        }
        finally
        {
            _reading = false;
        }
    }

    private void LayoutEditor()
    {
        var stacked = Root.ActualWidth < 850;

        if (_stacked == stacked)
        {
            return;
        }

        _stacked = stacked;
        EditorLayout.RowSpacing = stacked ? 14 : 0;
        EditorLayout.ColumnDefinitions.Clear();
        EditorLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        if (!stacked)
        {
            EditorLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(330) });
        }

        Grid.SetColumn(OptionsPanel, stacked ? 0 : 1);
        Grid.SetRow(OptionsPanel, stacked ? 1 : 0);
    }

    private void SetActionsEnabled(bool enabled)
    {
        GraphHost.IsEnabled = enabled;
        FanControlPanel.IsEnabled = enabled;
        CurvePointsPanel.IsEnabled = enabled;

        foreach (var button in Actions.Children.OfType<Button>())
        {
            var canRestoreWithoutBackend = ReferenceEquals(button, RestoreDefaultsButton) && !_busy;
            button.IsEnabled = enabled || canRestoreWithoutBackend;
        }
    }

    private async Task ReloadAsync()
    {
        if (_busy || _closed)
        {
            return;
        }

        _busy = true;
        SetActionsEnabled(false);

        try
        {
            var baseline = await _api.GetConfigurationAsync(_stop.Token);

            if (!_closed)
            {
                Load(baseline);
                EditorError.IsOpen = false;
            }
        }
        catch (Exception exception) when (IsRequestError(exception))
        {
            if (!_closed)
            {
                ShowError(exception.Message);
            }
        }
        finally
        {
            _busy = false;

            if (!_closed)
            {
                SetActionsEnabled(_baseline is not null);
            }
        }
    }

    private void Load(ConfigurationSnapshot baseline)
    {
        if (!baseline.Configuration.Plugins.TryGetValue("msi-psu", out var settings))
        {
            throw new InvalidDataException("The backend does not have the MSI PSU plugin.");
        }

        var options = settings.Options?.Deserialize(ClientJsonContext.Default.MsiOptions) ?? new MsiOptions();
        options.Validate();
        _baseline = baseline;
        _model.Edited -= OnEdited;
        _model = new MsiSettingsEditorViewModel(options with { FanControl = MsiFanControl.Curve });
        _model.Edited += OnEdited;
        Bindings.Update();
        _graph.SetModel(Model);
    }

    private void OnEdited() => EditorError.IsOpen = false;

    private async Task SaveAsync()
    {
        if (_busy || _closed || _baseline is null)
        {
            return;
        }

        _busy = true;
        SetActionsEnabled(false);

        try
        {
            var settings = _baseline.Configuration.Plugins["msi-psu"];
            var options = Model.Build();
            var version = _baseline.Version ?? throw new InvalidDataException("Reload configuration before applying.");

            if (options.FanControl == MsiFanControl.Curve && settings.PollIntervalMs > 5000)
            {
                throw new ArgumentException("Set MSI polling to 1000-5000 ms in Configuration before applying a fan curve.");
            }

            var plugins = new Dictionary<string, PluginSettings>(_baseline.Configuration.Plugins, StringComparer.Ordinal)
            {
                ["msi-psu"] = settings with { Options = JsonSerializer.SerializeToElement(options, ClientJsonContext.Default.MsiOptions) }
            };

            var saved = await _api.SetConfigurationAsync(_baseline.Configuration with { Plugins = plugins },
                version, _stop.Token);
            _onSaved(saved, options.FanCurve, version);

            if (!_closed)
            {
                Load(saved);
                EditorError.IsOpen = false;
            }
        }
        catch (Exception exception) when (IsRequestError(exception))
        {
            if (!_closed)
            {
                ShowError(exception.Message + " Reload if the service configuration changed elsewhere.");
            }
        }
        finally
        {
            _busy = false;

            if (!_closed)
            {
                SetActionsEnabled(_baseline is not null);
            }
        }
    }

    private void AddPoint_Click(object sender, RoutedEventArgs args) => Model.AddCurvePoint();

    private void RemovePoint_Click(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: MsiFanCurvePointViewModel point })
        {
            Model.RemoveCurvePoint(point);
        }
    }

    private void ShowError(string message)
    {
        EditorError.Message = message;
        EditorError.IsOpen = true;
    }

    private void IntegerValue_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (double.IsFinite(args.NewValue))
        {
            var rounded = Math.Round(args.NewValue, MidpointRounding.AwayFromZero);

            if (sender.Tag as string == "FanDuty" && rounded is >= 0 and < 25)
            {
                rounded = 0;
            }

            if (args.NewValue != rounded)
            {
                sender.Value = rounded;
            }
        }
    }

    private void RestoreDefaults_Click(object sender, RoutedEventArgs args)
    {
        _restoreDefaults();
        Close();
    }

    private async void Apply_Click(object sender, RoutedEventArgs args) => await SaveAsync();
    private async void Reload_Click(object sender, RoutedEventArgs args) => await ReloadAsync();

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _closed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _stop.Cancel();
        _model.Edited -= OnEdited;
        _graph.Dispose();
        _stop.Dispose();
    }

    private static bool IsRequestError(Exception exception) => exception is MonitoringApiException or HttpRequestException or IOException
        or ArgumentException or InvalidOperationException or NotSupportedException or OperationCanceledException or JsonException;
}

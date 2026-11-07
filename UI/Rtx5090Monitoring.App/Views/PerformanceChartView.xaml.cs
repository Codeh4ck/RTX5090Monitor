using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using Rtx5090Monitoring.App.Services;
using Rtx5090Monitoring.Presentation.Performance;
using Windows.Storage.Pickers;

namespace Rtx5090Monitoring.App.Views;

public sealed partial class PerformanceChartView : UserControl
{
    private readonly AppRuntime _runtime;
    private readonly PerformanceRecorder _recorder;
    private readonly DispatcherQueueTimer _timer;
    private readonly Dictionary<string, int> _cursors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CheckBox> _choices = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StackPanel> _groups = new(StringComparer.Ordinal);
    private string? _sessionId;
    private bool _chartReady;
    private bool _unloaded;
    private Task? _initialization;
    private Task? _closing;
    private bool _exporting;
    private bool _importing;

    internal PerformanceChartView(AppRuntime runtime)
    {
        _runtime = runtime;
        _recorder = runtime.Performance;
        InitializeComponent();

        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(500);
        _timer.Tick += (_, _) => Refresh();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ActualThemeChanged += OnThemeChanged;
        SizeChanged += (_, _) =>
        {
            SourcesColumn.Width = new GridLength(Math.Clamp(ActualWidth * 0.35, 280, 380));
            var narrow = ActualWidth < 1000;
            SessionActions.RowSpacing = narrow ? 8 : 0;
            var buttons = new[] { StartButton, StopButton, ImportButton, ExportButton };
            for (var index = 0; index < buttons.Length; index++)
            {
                Grid.SetRow(buttons[index], narrow ? index / 2 : 0);
                Grid.SetColumn(buttons[index], narrow ? index % 2 : index);
            }
        };
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (_unloaded)
        {
            return;
        }

        Refresh();
        _timer.Start();
        _initialization ??= InitializeChartAsync();
        await _initialization;
    }

    private async Task InitializeChartAsync()
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Rtx5090Monitoring", "PerformanceChart.WebView2");
            var environment = await CoreWebView2Environment.CreateWithOptionsAsync(null, folder, new CoreWebView2EnvironmentOptions());
            if (_unloaded)
            {
                return;
            }

            await Chart.EnsureCoreWebView2Async(environment);
            if (_unloaded)
            {
                return;
            }

            var core = Chart.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
            core.NewWindowRequested += (_, eventArgs) => eventArgs.Handled = true;
            core.NavigationStarting += (_, eventArgs) =>
            {
                var initialHtml = !_chartReady && eventArgs.Uri.StartsWith("data:text/html;", StringComparison.OrdinalIgnoreCase);
                eventArgs.Cancel = eventArgs.Uri != "about:blank" && !initialHtml;
                if (eventArgs.Cancel)
                {
                    ShowNotice("External navigation is unavailable in the chart.");
                }
            };
            core.WebMessageReceived += (_, message) =>
            {
                if (message.TryGetWebMessageAsString() == "ready" && !_unloaded)
                {
                    _chartReady = true;
                    _sessionId = null;
                    SendAppearance();
                    Refresh();
                }
            };

            Chart.NavigateToString(PerformanceHtmlExporter.EmptyChart());
        }
        catch (Exception error) when (error is System.Runtime.InteropServices.COMException or InvalidOperationException
            or IOException or UnauthorizedAccessException)
        {
            if (!_unloaded)
            {
                ShowNotice($"The chart could not open. Recording and HTML export remain available. {error.Message}");
            }
        }
    }

    private async void OnUnloaded(object sender, RoutedEventArgs args) => await CloseAsync();

    internal Task CloseAsync()
    {
        _unloaded = true;
        _chartReady = false;
        _timer.Stop();
        ActualThemeChanged -= OnThemeChanged;
        return _closing ??= CloseChartAsync();
    }

    private async Task CloseChartAsync()
    {
        if (_initialization is not null)
        {
            await _initialization;
        }

        Chart.Close();
    }

    private void Refresh()
    {
        if (_unloaded)
        {
            return;
        }

        try
        {
            var frame = _recorder.ReadFrame(_sessionId, _cursors, includeSamples: _chartReady);
            _sessionId = frame.SessionId;
            UpdateSources(frame);

            var elapsed = TimeSpan.FromSeconds(frame.DurationSeconds);
            var duration = $"{(long)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
            SessionStatus.Text = $"{frame.Status}  {duration}  ·  {frame.SampleCount:N0} readings";
            StartButton.Content = frame.SampleCount > 0 && !frame.Recording ? "New recording" : "Start recording";
            StartButton.IsEnabled = !_importing && !frame.Recording && frame.Series.Any(series => series.Selected && series.Available);
            StopButton.IsEnabled = frame.Recording;
            ExportButton.IsEnabled = !_exporting && !_importing && !frame.Recording && frame.SampleCount > 0;
            ImportButton.IsEnabled = !_importing && !frame.Recording;

            if (_chartReady)
            {
                Chart.CoreWebView2.PostWebMessageAsJson(PerformanceHtmlExporter.SerializeFrame(frame));
            }
        }
        catch (Exception error) when (error is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            _chartReady = false;
            _sessionId = null;
            ShowNotice($"The chart is unavailable. Reopen this tab to reload it. {error.Message}");
        }
    }

    private void UpdateSources(PerformanceChartFrame frame)
    {
        var visibleKeys = frame.Series.Select(series => series.Trace.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var key in _choices.Keys.Where(key => !visibleKeys.Contains(key)).ToArray())
        {
            var choice = _choices[key];
            if (choice.Parent is Panel parent)
            {
                parent.Children.Remove(choice);
            }

            _choices.Remove(key);
        }

        foreach (var (name, group) in _groups.Where(pair => pair.Value.Children.Count == 0).ToArray())
        {
            var expander = SourcesHost.Children.OfType<Expander>().First(item => ReferenceEquals(item.Content, group));
            SourcesHost.Children.Remove(expander);
            _groups.Remove(name);
        }

        EmptySources.Visibility = frame.Series.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var series in frame.Series)
        {
            var trace = series.Trace;
            if (!_choices.TryGetValue(trace.Key, out var choice))
            {
                if (!_groups.TryGetValue(trace.Group, out var group))
                {
                    group = new StackPanel { Spacing = 5 };
                    var expander = new Expander
                    {
                        Header = trace.Group,
                        Content = group,
                        IsExpanded = true,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        HorizontalContentAlignment = HorizontalAlignment.Stretch,
                        Padding = new Thickness(8)
                    };
                    _groups.Add(trace.Group, group);
                    SourcesHost.Children.Add(expander);
                }

                var color = Windows.UI.Color.FromArgb(255, Convert.ToByte(trace.Color[1..3], 16),
                    Convert.ToByte(trace.Color[3..5], 16), Convert.ToByte(trace.Color[5..7], 16));
                var label = new Grid { ColumnSpacing = 7, HorizontalAlignment = HorizontalAlignment.Stretch };
                label.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                label.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                label.Children.Add(new Border
                {
                    Background = new SolidColorBrush(color),
                    Width = 12,
                    Height = 12,
                    CornerRadius = new CornerRadius(2),
                    VerticalAlignment = VerticalAlignment.Center
                });
                var text = new TextBlock { Text = trace.Label, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis };
                Grid.SetColumn(text, 1);
                label.Children.Add(text);

                choice = new CheckBox
                {
                    Content = label,
                    IsChecked = series.Selected,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch
                };
                ToolTipService.SetToolTip(choice, $"{trace.Label}\n{trace.DeviceId}");
                var key = trace.Key;
                choice.Checked += (_, _) => Select(key, true);
                choice.Unchecked += (_, _) => Select(key, false);
                _choices.Add(key, choice);
                group.Children.Add(choice);
            }

            choice.IsChecked = series.Selected;
            choice.Opacity = series.Available ? 1 : 0.55;
            ToolTipService.SetToolTip(choice, $"{trace.Label}\n{trace.DeviceId}" + (series.Available ? "" : "\nCurrently unavailable"));
        }
    }

    private void Select(string key, bool selected)
    {
        _recorder.Select(key, selected);
    }

    private void Start_Click(object sender, RoutedEventArgs args)
    {
        try
        {
            _recorder.Start();
            Notice.IsOpen = false;
        }
        catch (InvalidOperationException error)
        {
            ShowNotice(error.Message);
        }

        Refresh();
    }

    private void Stop_Click(object sender, RoutedEventArgs args)
    {
        _recorder.Stop();
        Refresh();
    }

    private async void Export_Click(object sender, RoutedEventArgs args)
    {
        if (_exporting)
        {
            return;
        }

        _exporting = true;
        ExportButton.IsEnabled = false;
        try
        {
            var session = _recorder.CompletedSession();
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = $"RTX-Monitor-{session.StartedAt:yyyyMMdd-HHmmss}"
            };
            picker.FileTypeChoices.Add("RTX Monitor recording", new List<string> { ".rtxchart" });
            picker.FileTypeChoices.Add("Interactive HTML chart", new List<string> { ".html" });
            WinRT.Interop.InitializeWithWindow.Initialize(picker, _runtime.MainWindowHandle);
            var file = await picker.PickSaveFileAsync();
            if (file is not null)
            {
                await Task.Run(() =>
                {
                    var contents = Path.GetExtension(file.Path).Equals(".html", StringComparison.OrdinalIgnoreCase)
                        ? PerformanceHtmlExporter.Export(session) : PerformanceRecordingFile.Serialize(session);
                    File.WriteAllText(file.Path, contents);
                }, _runtime.StopToken);
                ShowNotice($"Exported {file.Name}", InfoBarSeverity.Success);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException
            or ArgumentException or OperationCanceledException or System.Runtime.InteropServices.COMException)
        {
            ShowNotice(error.Message);
        }
        finally
        {
            _exporting = false;
            if (!_unloaded)
            {
                Refresh();
            }
        }
    }

    private async void Import_Click(object sender, RoutedEventArgs args)
    {
        if (_importing)
        {
            return;
        }

        _importing = true;
        Refresh();
        try
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
            picker.FileTypeFilter.Add(".rtxchart");
            picker.FileTypeFilter.Add(".html");
            picker.FileTypeFilter.Add(".json");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, _runtime.MainWindowHandle);
            var file = await picker.PickSingleFileAsync();
            if (file is not null)
            {
                var session = await Task.Run(async () =>
                {
                    if (new FileInfo(file.Path).Length > PerformanceRecordingFile.MaximumFileBytes)
                    {
                        throw new InvalidDataException("The recording file exceeds 256 MB.");
                    }

                    var contents = await File.ReadAllTextAsync(file.Path, _runtime.StopToken);
                    return PerformanceRecordingFile.Deserialize(contents);
                }, _runtime.StopToken);

                _recorder.Import(session);
                _choices.Clear();
                _groups.Clear();
                SourcesHost.Children.Clear();
                _sessionId = null;
                ShowNotice($"Imported {file.Name}", InfoBarSeverity.Success);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException
            or ArgumentException or OperationCanceledException or System.Runtime.InteropServices.COMException)
        {
            ShowNotice(error.Message);
        }
        finally
        {
            _importing = false;
            if (!_unloaded)
            {
                Refresh();
            }
        }
    }

    private void OnThemeChanged(FrameworkElement sender, object args) => SendAppearance();

    private void SendAppearance()
    {
        if (_chartReady)
        {
            Chart.CoreWebView2.PostWebMessageAsJson(ActualTheme == ElementTheme.Light
                ? "{\"type\":\"appearance\",\"light\":true}"
                : "{\"type\":\"appearance\",\"light\":false}");
        }
    }

    private void ShowNotice(string message, InfoBarSeverity severity = InfoBarSeverity.Warning)
    {
        Notice.Message = message;
        Notice.Severity = severity;
        Notice.IsOpen = true;
    }
}

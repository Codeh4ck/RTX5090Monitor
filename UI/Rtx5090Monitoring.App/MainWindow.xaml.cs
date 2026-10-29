using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Windowing;
using Rtx5090Monitoring.App.Platform.Windowing;
using Rtx5090Monitoring.App.Services;
using Rtx5090Monitoring.App.Views;
using Rtx5090Monitoring.Presentation.Telemetry;
using Rtx5090Monitoring.Presentation.Settings;

namespace Rtx5090Monitoring.App;

public sealed partial class MainWindow : Window
{
    private readonly AppRuntime _runtime;
    private readonly nint _windowHandle;
    private string _page = "hardware";
    private string? _panelId;
    private bool _rendering = true;
    private bool _startupTabPending = true;
    private bool _rememberWindowSize;
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _sizeSaveTimer;
    public Rtx5090Monitoring.Presentation.Updates.UpdateViewModel Updates => _runtime.Updates;
    public DashboardViewModel Model { get; private set; }
    internal FrameworkElement ThemeRoot => Root;
    internal object? CurrentView => PageHost.Content;
    public Visibility WarningVisibility(bool hasWarning) => hasWarning ? Visibility.Visible : Visibility.Collapsed;

    internal void ApplyAppearance(AppPreferences preferences)
    {
        ThemeService.Apply(Root, preferences);
        PageHost.FontSize = 14 * preferences.TextScale;
        Tabs.FontSize = 14 * preferences.TextScale;
        AccentGlow.Visibility = preferences.ReducedEffects ? Visibility.Collapsed : Visibility.Visible;
    }

    internal void ShowPanel(string id)
    {
        Model.SelectedPanel = Model.ReportingPanels.First(panel => panel.Id == id);
        _panelId = id;
        _page = "hardware";
        RenderPage();
    }

    internal MainWindow(AppRuntime runtime)
    {
        _runtime = runtime;
        Model = runtime.Dashboard;
        InitializeComponent();
        _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = true;
            presenter.IsResizable = true;
        }

        _sizeSaveTimer = DispatcherQueue.CreateTimer();
        _sizeSaveTimer.Interval = TimeSpan.FromMilliseconds(400);
        _sizeSaveTimer.IsRepeating = false;
        _sizeSaveTimer.Tick += (_, _) => SaveNormalWindowSize();

        AppWindow.Changed += (_, args) =>
        {
            if (args.DidSizeChange && _rememberWindowSize
                && AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Restored })
            {
                _sizeSaveTimer.Stop();
                _sizeSaveTimer.Start();
            }
        };

        Closed += (_, _) => _sizeSaveTimer.Stop();
        runtime.OperationCompleted += message =>
        {
            OperationStatus.Text = message;
            ToolTipService.SetToolTip(OperationStatus, message);
        };
        if (runtime.IsDemo)
        {
            ModeLabel.Text = "DEMO · SIMULATED TELEMETRY";
        }

        Model.ReportingPanels.CollectionChanged += OnReportingPanelsChanged;
        SelectInitialPanel();
    }

    private void OnReportingPanelsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs args) => SelectInitialPanel();

    internal void RefreshRuntime(bool simulated)
    {
        Model.ReportingPanels.CollectionChanged -= OnReportingPanelsChanged;
        Model = _runtime.Dashboard;
        Model.ReportingPanels.CollectionChanged += OnReportingPanelsChanged;
        ModeLabel.Text = simulated ? "DEBUG SIMULATION ACTIVE" : _runtime.IsDemo ? "DEMO · SIMULATED TELEMETRY" : "POWER · PINS · PROTECTION";
        Bindings.Update();
        SelectInitialPanel();
        RenderPage();
    }

    internal void EnableWindowSizePersistence() => _rememberWindowSize = true;

    internal void SaveNormalWindowSize()
    {
        if (!_rememberWindowSize || AppWindow.Presenter is not OverlappedPresenter { State: OverlappedPresenterState.Restored })
        {
            return;
        }

        var scale = Math.Max(96, Platform.Interop.NativeMethods.GetDpiForWindow(_windowHandle)) / 96d;
        var size = AppWindow.Size;
        _runtime.Settings.SaveWindowSize(
            Math.Clamp(size.Width / scale, WindowSizeLimits.MinimumWidth, WindowSizeLimits.MaximumWidth),
            Math.Clamp(size.Height / scale, WindowSizeLimits.MinimumHeight, WindowSizeLimits.MaximumHeight));
    }

    internal void ApplyWindowSize()
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Restore();
        }

        var scale = Math.Max(96, Platform.Interop.NativeMethods.GetDpiForWindow(_windowHandle)) / 96d;
        var maximum = WindowSizeLimits.GetMaximumPixels(_windowHandle);
        AppWindow.Resize(new Windows.Graphics.SizeInt32(
            Math.Min(maximum.Width, (int)(_runtime.Preferences.WindowWidth * scale)),
            Math.Min(maximum.Height, (int)(_runtime.Preferences.WindowHeight * scale))));
    }

    private void SelectInitialPanel()
    {
        if (_page != "hardware")
        {
            return;
        }

        if (Model.ReportingPanels.Count > 0)
        {
            var preferred = _startupTabPending
                ? Model.ReportingPanels.FirstOrDefault(panel => panel.Id == _runtime.Preferences.StartupTabId) : null;
            var selected = preferred ?? Model.ReportingPanels.FirstOrDefault(panel => panel.Id == _panelId) ?? Model.ReportingPanels[0];

            if (preferred is not null)
            {
                _startupTabPending = false;
            }

            if (_panelId != selected.Id || Model.SelectedPanel != selected)
            {
                ShowPanel(selected.Id);
            }
        }
        else
        {
            Model.SelectedPanel = null;
            _panelId = null;
            RenderPage();
        }
    }

    private void HardwareTab_Click(object sender, RoutedEventArgs args)
    {
        _startupTabPending = false;
        var button = (ToggleButton)sender;
        ShowPanel((string)button.Tag);
        button.IsChecked = true;
    }

    private void Configuration_Click(object sender, RoutedEventArgs args) => Navigate("configuration");
    private void Settings_Click(object sender, RoutedEventArgs args) => Navigate("settings");

    internal void Navigate(string page)
    {
        _page = page;
        if (page != "hardware")
        {
            Model.SelectedPanel = null;
        }

        RenderPage();
    }

    internal void SetRendering(bool enabled)
    {
        _rendering = enabled;
        if (enabled)
        {
            RenderPage();
        }
        else
        {
            PageHost.Content = null;
        }
    }

    private void RenderPage()
    {
        if (!_rendering)
        {
            return;
        }

        PageHost.Content = _page switch
        {
            "settings" => new SettingsView(_runtime),
            "configuration" => new ConfigurationView(_runtime),
            _ when Model.ReportingPanels.FirstOrDefault(panel => panel.Id == _panelId) is { IsMsi: true } msi => new MsiHardwareView(msi, _runtime),
            _ when Model.ReportingPanels.FirstOrDefault(panel => panel.Id == _panelId) is { } panel => new HardwareView(panel, _runtime),
            _ => new TextBlock { Text = "No plugins are reporting. Open Configuration to check device and plugin settings.", TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(24) }
        };
    }
}

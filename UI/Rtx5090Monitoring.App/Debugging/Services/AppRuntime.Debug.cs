using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Rtx5090Monitoring.App.Debugging.Windows;
using Rtx5090Monitoring.Debugging;
using Rtx5090Monitoring.Debugging.Api;
using Rtx5090Monitoring.Debugging.Catalog;
using Rtx5090Monitoring.Debugging.Models;
using Rtx5090Monitoring.Presentation.Configuration;
using Rtx5090Monitoring.Presentation.Notifications;
using Rtx5090Monitoring.Presentation.Telemetry;

namespace Rtx5090Monitoring.App.Services;

internal sealed partial class AppRuntime
{
    private DebugMonitoringApi _debugApi = null!;
    private ConfigurationEditorViewModel _debugConfiguration = null!;
    private DashboardViewModel _debugDashboard = new();
    private WarningTransitionTracker _debugWarnings = new();
    private DispatcherQueueTimer _debugTimer = null!;
    private DebugWindow? _debugWindow;
    private DebugFrame? _debugFrame;
    private long _debugRevision = -1;
    private bool _wasDebugEnabled;
    private bool _publishingDebug;
    private Rtx5090Monitoring.Plugins.WireView.Contracts.Common.DeviceConfig? _liveWireViewConfiguration;
    public DebugSession Debug { get; } = new();
    internal DebugWindow? DebugConsole => _debugWindow;
    internal int DebugNotificationCount { get; private set; }

    private void InitializeDebugging()
    {
        _debugApi = new DebugMonitoringApi(Debug, TimeProvider.System);
        _debugConfiguration = new ConfigurationEditorViewModel(() => _debugApi, localOnly: true);
        _debugTimer = _dispatcher.CreateTimer();
        _debugTimer.Interval = TimeSpan.FromSeconds(1);
        _debugTimer.Tick += (_, _) => PublishDebugFrame();
        Debug.Changed += OnDebugChanged;
    }

    internal void OpenDebugConsole()
    {
        if (_debugWindow is not null)
        {
            _debugWindow.AppWindow.Show();
            _debugWindow.Activate();
            return;
        }
        _debugWindow = new DebugWindow(this);
        _debugWindow.Closed += (_, _) => _debugWindow = null;
        if (_window is { } main)
        {
            var area = DisplayArea.GetFromWindowId(main.AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
            var scale = main.ThemeRoot.XamlRoot?.RasterizationScale ?? 1;
            var width = Math.Min(area.Width, (int)(640 * scale));
            var height = Math.Min(area.Height, (int)(850 * scale));
            _debugWindow.AppWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
            var right = main.AppWindow.Position.X + main.AppWindow.Size.Width + 8;
            var left = main.AppWindow.Position.X - width - 8;
            var x = right + width <= area.X + area.Width ? right : left >= area.X ? left : area.X + area.Width - width;
            _debugWindow.AppWindow.Move(new Windows.Graphics.PointInt32(x, Math.Clamp(main.AppWindow.Position.Y, area.Y, area.Y + area.Height - height)));
        }
        _debugWindow.Activate();
    }

    private void OnDebugChanged(bool structural)
    {
        var transition = _wasDebugEnabled != Debug.Enabled;
        _wasDebugEnabled = Debug.Enabled;
        if (transition && Debug.Enabled)
        {
            _debugDashboard = new DashboardViewModel();
            _debugConfiguration = new ConfigurationEditorViewModel(() => _debugApi, localOnly: true);
            _debugWarnings = new WarningTransitionTracker();
            _debugRevision = -1;
            DebugNotificationCount = 0;
            _debugTimer.Start();
        }
        if (Debug.Enabled)
        {
            PublishDebugFrame();
        }
        else if (transition)
        {
            _debugTimer.Stop();
            _debugFrame = null;
        }
        if (transition)
        {
            _refresh = true;
            _rendered = null;
            _window?.RefreshRuntime(Debug.Enabled);
            RenderLatest();
            if (!Debug.Enabled && _session?.Latest is { } live && Preferences.WarningNotifications)
            {
                foreach (var warning in _warnings.Evaluate(live, DateTimeOffset.UtcNow, Preferences))
                {
                    ShowNotification(warning.Title, warning.Message);
                }
            }
        }
        else if (Debug.Enabled && _rendering)
        {
            RenderLatest();
        }
    }

    private void PublishDebugFrame()
    {
        if (!Debug.Enabled || _publishingDebug || StopToken.IsCancellationRequested)
        {
            return;
        }

        _publishingDebug = true;
        try
        {
            var now = DateTimeOffset.UtcNow;
            _debugFrame = Debug.Compose(now);
            _debugDashboard.SetCatalog(_debugFrame.Catalog);
            if (_debugRevision != _debugFrame.Configuration.Revision)
            {
                _debugRevision = _debugFrame.Configuration.Revision;
                _debugDashboard.SetConfiguration(_debugFrame.Configuration);
                _debugConfiguration.SetCatalog(_debugFrame.Catalog);
                _debugConfiguration.Receive(_debugFrame.Configuration);
            }
            _debugDashboard.Connection = "DEBUG SIMULATION ACTIVE · " + _liveDashboard.Connection;
            foreach (var warning in _debugWarnings.Evaluate(_debugFrame.Snapshot, now, Preferences))
            {
                if (!Preferences.WarningNotifications)
                {
                    continue;
                }

                DebugNotificationCount++;
                Debug.ReportNotification(warning.Title, warning.Message);
                ShowNotification("DEBUG · " + warning.Title, warning.Message);
            }
        }
        finally { _publishingDebug = false; }
    }

    private void StopDebugging()
    {
        _debugTimer.Stop();
        Debug.Changed -= OnDebugChanged;
        _debugWindow?.Close();
    }

    private void ApplyDebugDeviceConfiguration()
    {
        if (!Debug.Enabled || _debugFrame is null)
        {
            return;
        }

        var wire = _debugDashboard.Panels.FirstOrDefault(panel => panel.IsWireView);
        if (wire is not { IsReporting: true, DeviceDetails.Count: 0 })
        {
            return;
        }

        var configuration = Debug.Plugins[0].IsSimulated ? DebugDeviceDefaults.WireView() : _liveWireViewConfiguration;
        if (configuration is null)
        {
            return;
        }

        wire.SetDeviceConfiguration(configuration);
        wire.Apply(_debugFrame.Snapshot, Preferences, DateTimeOffset.UtcNow);
    }
}

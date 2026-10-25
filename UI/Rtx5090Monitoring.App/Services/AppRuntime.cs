using Microsoft.UI.Dispatching;
using Rtx5090Monitoring.App.Platform.Startup;
using Rtx5090Monitoring.App.Platform.Tray;
using Rtx5090Monitoring.Client.Http;
using Rtx5090Monitoring.Client.Streaming;
using Rtx5090Monitoring.Plugins.WireView.Contracts.Input;
using Rtx5090Monitoring.Presentation.Configuration;
using Rtx5090Monitoring.Presentation.Notifications;
using Rtx5090Monitoring.Presentation.Settings;
using Rtx5090Monitoring.Presentation.Telemetry;

namespace Rtx5090Monitoring.App.Services;

internal sealed partial class AppRuntime
{
    private readonly CancellationTokenSource _stop = new();
    private readonly PreferencesStore _store;
    private readonly bool _diagnostic;
    private readonly DispatcherQueue _dispatcher;
    private readonly SemaphoreSlim _connectionGate = new(1);
    private readonly DispatcherQueueTimer _timer;
    private readonly WarningTransitionTracker _warnings = new();
    private HttpClient? _http;
    private IMonitoringApi _api = null!;
    private MonitoringSession? _session;
    private CancellationTokenSource? _connectionStop;
    private Task? _connectionTask;
    private MainWindow? _window;
    private TrayService? _tray;
    private bool _rendering = true;
    private bool _refresh = true;
    private bool _connected;
    private string? _address;
    private bool _startupEnabled;
    private Contracts.Sensors.SensorSnapshot? _rendered;

    public bool IsDemo { get; }
    public AppPreferences Preferences => Settings.Value;
    public AppSettingsViewModel Settings { get; }
    private readonly DashboardViewModel _liveDashboard = new();
    private readonly ConfigurationEditorViewModel _liveConfiguration;
    public DashboardViewModel Dashboard =>
#if TELEMETRY_DEBUG
        Debug.Enabled ? _debugDashboard :
#endif
        _liveDashboard;
    public ConfigurationEditorViewModel Configuration =>
#if TELEMETRY_DEBUG
        Debug.Enabled ? _debugConfiguration :
#endif
        _liveConfiguration;
    private IMonitoringApi CurrentApi =>
#if TELEMETRY_DEBUG
        Debug.Enabled ? _debugApi :
#endif
        _api;
    public CancellationToken StopToken => _stop.Token;
    internal long LatestSequence => _session?.Latest?.Sequence ?? 0;
    internal long RenderTicks { get; private set; }
    internal bool TrayAvailable => _tray?.IsAvailable == true;
    internal event Action<string>? OperationCompleted;

    public AppRuntime(bool demo, DispatcherQueue dispatcher, bool diagnostic = false)
    {
#if TELEMETRY_DEBUG
        IsDemo = demo;
#else
        IsDemo = false;
#endif
        _dispatcher = dispatcher;
        _diagnostic = diagnostic;
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Rtx5090Monitoring");
        _store = new PreferencesStore(Path.Combine(directory, diagnostic ? "smoke-preferences.json" : demo ? "demo-preferences.json" : "preferences.json"));
        Settings = new AppSettingsViewModel(_store.Load());
        _startupEnabled = Settings.Value.StartWithWindows;
        Settings.Message = _store.LastError ?? string.Empty;
        Settings.Changed += PreferencesChanged;
        InitializeUpdates(directory);
        _liveConfiguration = new ConfigurationEditorViewModel(() => _api);
        _timer = dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(250);
        _timer.Tick += (_, _) => RenderLatest();
#if TELEMETRY_DEBUG
        InitializeDebugging();
#endif
    }

    public async Task StartAsync(MainWindow window, TrayService tray)
    {
        _window = window;
        _tray = tray;
        window.ThemeRoot.ActualThemeChanged += (_, _) => window.ApplyAppearance(Preferences);
        window.ApplyAppearance(Preferences);
        if (!IsDemo && Preferences.StartWithWindows)
        {
            try
            {
                StartupRegistration.SetEnabled(true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                Settings.Message = $"Windows startup registration could not be updated: {exception.Message}";
            }
        }

        await ConnectAsync();
        _timer.Start();
        StartUpdates();
    }

    public void Reconnect() => _session?.Reconnect();
    public void SetPinMetric(PinMetric metric) => Settings.PinMetric = metric;
    internal void SetDemoDeviceAvailable(string pluginId, bool available)
    {
#if TELEMETRY_DEBUG
        if (_api is not Demo.DemoMonitoringApi demo)
        {
            throw new InvalidOperationException("Simulated devices are available only in demo mode.");
        }

        demo.SetDeviceAvailable(pluginId, available);
#else
        throw new InvalidOperationException("Simulation is not compiled into this build.");
#endif
    }

    public void SetRendering(bool enabled)
    {
        _rendering = enabled;
        _window?.SetRendering(enabled);
        if (enabled)
        {
            _refresh = true;
            RenderLatest();
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    private async void PreferencesChanged(AppPreferences preferences)
    {
        try
        {
            ConfigureUpdates();
            _ = Updates.CheckAsync(false, StopToken);
            _window?.ApplyAppearance(preferences);
            _wireViewWindow?.ApplyAppearance(preferences);
#if TELEMETRY_DEBUG
            _debugWindow?.ApplyAppearance(preferences);
#endif
            _refresh = true;
            if (!IsDemo && _startupEnabled != preferences.StartWithWindows)
            {
                StartupRegistration.SetEnabled(preferences.StartWithWindows);
                _startupEnabled = preferences.StartWithWindows;
            }
            await _store.SaveAsync(preferences, StopToken);
            if (_address != preferences.ServiceAddress)
            {
                await ConnectAsync();
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException or OperationCanceledException)
        {
            Settings.Message = exception.Message;
        }
    }

    private async Task ConnectAsync()
    {
        await _connectionGate.WaitAsync(StopToken);
        try
        {
            _connectionStop?.Cancel();
            if (_connectionTask is not null)
            {
                await _connectionTask;
            }

            _connectionStop?.Dispose();
            _http?.Dispose();
            _address = Preferences.ServiceAddress;
            _http = new HttpClient { BaseAddress = new Uri(_address), Timeout = TimeSpan.FromSeconds(8) };
            _api =
#if TELEMETRY_DEBUG
                IsDemo ? new Demo.DemoMonitoringApi() :
#endif
                new MonitoringApiClient(_http);
            var session = new MonitoringSession(_api);
            _session = session;
            session.StatusChanged += status => Dispatch(session, () =>
            {
                var connectedNow = status.Connected && !_connected;
                _connected = status.Connected;

                if (connectedNow)
                {
                    _ = Updates.RefreshServiceAsync(StopToken);
                }

                _liveDashboard.Connection = status.Message;
                _tray?.SetStatus(status.Message);
                if (!status.Connected && _rendering)
                {
                    _liveDashboard.Disconnect(Preferences);
                }
#if TELEMETRY_DEBUG
                if (!status.Connected)
                {
                    Debug.ReceiveSnapshot(null);
                }

                if (Debug.Enabled)
                {
                    PublishDebugFrame();
                }
#endif

                _refresh = true;
            });
            session.CatalogReceived += catalog => Dispatch(session, () =>
            {
                _liveDashboard.SetCatalog(catalog);
                _liveConfiguration.SetCatalog(catalog);
#if TELEMETRY_DEBUG
                Debug.ReceiveCatalog(catalog);
                if (Debug.Enabled)
                {
                    PublishDebugFrame();
                }
#endif
            });
            session.ConfigurationReceived += configuration => Dispatch(session, () =>
            {
                _liveDashboard.SetConfiguration(configuration);
                _liveConfiguration.Receive(configuration);
#if TELEMETRY_DEBUG
                Debug.ReceiveConfiguration(configuration);
                if (Debug.Enabled)
                {
                    PublishDebugFrame();
                }
#endif
            });
            session.SnapshotReceived += snapshot =>
            {
                var notifications =
#if TELEMETRY_DEBUG
                    Debug.Enabled ? [] :
#endif
                    _warnings.Evaluate(snapshot, DateTimeOffset.UtcNow, Preferences);
#if TELEMETRY_DEBUG
                Dispatch(session, () =>
                {
                Debug.ReceiveSnapshot(snapshot);
                    if (Debug.Enabled)
                    {
                        PublishDebugFrame();
                    }
                });
                if (Debug.Enabled)
                {
                    return;
                }
#endif
                if (notifications.Length > 0)
                {
                    Dispatch(session, () =>
                    {
                        if (Preferences.WarningNotifications
#if TELEMETRY_DEBUG
                            && !Debug.Enabled
#endif
                            )
                        {
                            foreach (var warning in notifications)
                            {
                                ShowNotification(warning.Title, warning.Message);
                            }
                        }
                    });
                }
            };
            _connectionStop = CancellationTokenSource.CreateLinkedTokenSource(StopToken);
            _connectionTask = Task.Run(() => session.RunAsync(_connectionStop.Token), StopToken);
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    private void Dispatch(MonitoringSession session, Action action) => _dispatcher.TryEnqueue(() =>
    {
        if (_session == session && !StopToken.IsCancellationRequested)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "rtx-monitor-error.txt"), exception.ToString());
                throw;
            }
        }
    });

    private void RenderLatest()
    {
        if (!_rendering)
        {
            return;
        }

        RenderTicks++;
        var snapshot =
#if TELEMETRY_DEBUG
            Debug.Enabled ? _debugFrame?.Snapshot :
#endif
            _session?.Latest;
        if (snapshot is not null && (_refresh || !ReferenceEquals(snapshot, _rendered)))
        {
            Dashboard.Apply(snapshot, Preferences, DateTimeOffset.UtcNow);
#if TELEMETRY_DEBUG
            ApplyDebugDeviceConfiguration();
#endif
            Configuration.SetAvailableSensors(snapshot);
            _rendered = snapshot;
        }
        else if (!_connected
#if TELEMETRY_DEBUG
            && !Debug.Enabled
#endif
            )
        {
            Dashboard.Disconnect(Preferences);
        }
        else
        {
            Dashboard.RefreshFreshness(DateTimeOffset.UtcNow, Preferences);
        }

        _wireViewWindow?.SetPanel(Dashboard.Panels.FirstOrDefault(panel => panel.IsWireView));
        _refresh = false;
    }

    public Task RestartPluginAsync(string id) => PerformAsync(async () =>
    {
        await CurrentApi.RestartPluginAsync(id, StopToken);
        Configuration.Message = $"Restart requested for {id}";
        OperationCompleted?.Invoke(Configuration.Message);
    });

    public Task ReadWireViewDeviceAsync() => PerformAsync(async () =>
    {
        var panel = Dashboard.Panels.FirstOrDefault(item => item.IsWireView);
        var api = CurrentApi;
        var device = panel?.DeviceText;
        panel?.SetDeviceConfigurationStatus("Reading device settings...");
        try
        {
            var configuration = await api.GetWireViewConfigurationAsync(StopToken);
            if (panel is not { IsReporting: true } || panel.DeviceText != device || !ReferenceEquals(api, CurrentApi)
                || !ReferenceEquals(panel, Dashboard.Panels.FirstOrDefault(item => item.IsWireView)))
            {
                return;
            }
#if TELEMETRY_DEBUG
            if (!Debug.Enabled)
            {
                _liveWireViewConfiguration = configuration;
            }
#endif
            panel?.SetDeviceConfiguration(configuration);
            OperationCompleted?.Invoke("WireView device settings read");
        }
        catch (Exception exception) when (exception is HttpRequestException or MonitoringApiException or IOException
            or OperationCanceledException or System.Text.Json.JsonException)
        {
            panel?.SetDeviceConfigurationStatus("Could not read device settings: " + exception.Message);
            throw;
        }
    });

    public Task ClearFaultsAsync(bool status, bool log) => PerformAsync(async () =>
    {
        if (!status && !log)
        {
            return;
        }

        await CurrentApi.ClearWireViewFaultsAsync(new ClearFaultsRequest
        {
            FaultStatusMask = status ? ushort.MaxValue : (ushort)0,
            FaultLogMask = log ? ushort.MaxValue : (ushort)0
        }, StopToken);
        Configuration.Message = "Fault-clear command acknowledged by WireView";
        OperationCompleted?.Invoke(Configuration.Message);
    });

    public Task RefreshDevicesAsync() => PerformAsync(async () =>
    {
        Configuration.SetDevices(await CurrentApi.GetAsusDevicesAsync(StopToken));
        Configuration.Message = "GPU list refreshed";
        OperationCompleted?.Invoke(Configuration.Message);
    });

    public Task RefreshMsiDevicesAsync() => PerformAsync(async () =>
    {
        Configuration.SetMsiDevices(await CurrentApi.GetMsiDevicesAsync(StopToken));
        Configuration.Message = "PSU list refreshed";
        OperationCompleted?.Invoke(Configuration.Message);
    });

    public Task RestartServiceAsync() => PerformAsync(async () =>
    {
        await CurrentApi.RestartServiceAsync(StopToken);
        OperationCompleted?.Invoke("Backend service restart requested");
#if TELEMETRY_DEBUG
        if (Debug.Enabled)
        {
            return;
        }
#endif
        Reconnect();
    });

    private void ShowNotification(string title, string message)
    {
        if (!_diagnostic)
        {
            _tray?.Notify(title, message);
        }
    }

    private async Task PerformAsync(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (Exception exception) when (exception is HttpRequestException or MonitoringApiException or IOException
            or OperationCanceledException or System.Text.Json.JsonException)
        {
            Configuration.Message = exception.Message;
            Settings.Message = exception.Message;
            OperationCompleted?.Invoke(exception.Message);
            ShowNotification("RTX Monitor · operation failed", exception.Message);
        }
    }

    public async Task StopAsync()
    {
        _timer.Stop();
        _updateTimer?.Stop();
        Updates.Cancel();
        _updateHttp.Dispose();
        _wireViewWindow?.Close();
#if TELEMETRY_DEBUG
        StopDebugging();
#endif
        Settings.Changed -= PreferencesChanged;
        _stop.Cancel();
        if (_connectionTask is not null)
        {
            try { await _connectionTask; }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        }
        _http?.Dispose();
        await _store.SaveAsync(Preferences);
    }
}

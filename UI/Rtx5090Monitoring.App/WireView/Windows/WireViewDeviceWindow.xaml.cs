using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Rtx5090Monitoring.App.Controls;
using Rtx5090Monitoring.App.Services;
using Rtx5090Monitoring.Presentation.Settings;
using Rtx5090Monitoring.Presentation.Telemetry;

namespace Rtx5090Monitoring.App.WireView.Windows;

internal sealed partial class WireViewDeviceWindow : Window
{
    private readonly AppRuntime _runtime;
    private HardwarePanelViewModel? _model;
    private bool _refreshing;
    private bool _queued;
    private bool _closed;
    internal FrameworkElement ThemeRoot => Root;

    internal WireViewDeviceWindow(AppRuntime runtime)
    {
        _runtime = runtime;
        InitializeComponent();
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "monitor.ico"));
        ApplyAppearance(runtime.Preferences);
        SetPanel(runtime.Dashboard.Panels.FirstOrDefault(panel => panel.IsWireView));
        Closed += OnClosed;
    }

    internal void ApplyAppearance(AppPreferences preferences) => ThemeService.Apply(Root, preferences);

    internal void SetPanel(HardwarePanelViewModel? model)
    {
        if (ReferenceEquals(model, _model))
        {
            return;
        }

        Unsubscribe();
        _model = model;
        if (_model is not null)
        {
            _model.PropertyChanged += OnModelChanged;
            _model.DeviceDetails.CollectionChanged += OnDetailsChanged;
        }
        RefreshRows();
    }

    internal async Task RefreshAsync()
    {
        if (_closed || _refreshing || _model is not { IsReporting: true })
        {
            return;
        }

        _refreshing = true;
        UpdateStatus();
        try
        {
            await _runtime.ReadWireViewDeviceAsync();
        }
        finally
        {
            _refreshing = false;
            if (!_closed)
            {
                UpdateStatus();
            }
        }
    }

    private void OnDetailsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (_queued || _closed)
        {
            return;
        }
        _queued = Root.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            _queued = false;
            if (!_closed)
            {
                RefreshRows();
            }
        });
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(HardwarePanelViewModel.DeviceConfigurationStatus) or nameof(HardwarePanelViewModel.IsReporting)
            or nameof(HardwarePanelViewModel.DeviceText))
        {
            UpdateStatus();
        }
    }

    private void RefreshRows()
    {
        RowsHost.Children.Clear();
        if (_model is not null)
        {
            foreach (var metric in _model.DeviceDetails)
            {
                RowsHost.Children.Add(new MetricTile(metric, showIcon: false));
            }
        }
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        RefreshButton.IsEnabled = !_refreshing && _model is { IsReporting: true };
        StatusText.Text = _model is null ? "WireView is not available." : _model.IsReporting
            ? _model.DeviceConfigurationStatus : "WireView is not reporting. Refresh after reconnecting.";
    }

    private void Unsubscribe()
    {
        if (_model is not null)
        {
            _model.PropertyChanged -= OnModelChanged;
            _model.DeviceDetails.CollectionChanged -= OnDetailsChanged;
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _closed = true;
        Unsubscribe();
        RowsHost.Children.Clear();
        _model = null;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs args) => await RefreshAsync();
}

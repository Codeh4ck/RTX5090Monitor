using System.ComponentModel;
using System.Collections.Specialized;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rtx5090Monitoring.App.Controls;
using Rtx5090Monitoring.App.Services;
using Rtx5090Monitoring.Presentation.Telemetry;

namespace Rtx5090Monitoring.App.Views;

public sealed partial class HardwareView : UserControl
{
    private readonly AppRuntime _runtime;
    private bool? _wide;
    private bool _showPins;
    public HardwarePanelViewModel Model { get; }
    internal bool IsResetPeakVisible => ResetPeakButton.Visibility == Visibility.Visible;
    public string DetailsHeading => Model.IsWireView ? "Temperatures and faults" : "GPU status";
    public string DeviceIconName => Model.IsAsus ? "asus" : "pin";
    public string DetailsIconName => Model.IsWireView ? "vtemp" : "vcore";

    internal HardwareView(HardwarePanelViewModel model, AppRuntime runtime)
    {
        Model = model;
        _runtime = runtime;
        InitializeComponent();
        PinHost.Children.Add(new PinAxis(Model.Pins[0]));
        foreach (var pin in Model.Pins)
        {
            var bar = new PinBar(pin);
            Grid.SetColumn(bar, pin.Channel);
            PinHost.Children.Add(bar);
        }

        SizeChanged += OnSizeChanged;
        Model.PropertyChanged += OnModelChanged;
        Model.Metrics.CollectionChanged += RebuildMetrics;
        Model.Faults.CollectionChanged += RebuildFaults;
        Unloaded += OnUnloaded;
        RebuildMetrics(null, null);
        RebuildFaults(null, null);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs args)
    {
        UpdateLayoutColumns();
        LayoutHeadlines();
    }

    private void LayoutHeadlines()
    {
        var columns = ActualWidth >= 1000 && Model.IsAsus ? 5 : ActualWidth >= 650 ? 3 : 2;
        var cards = HeadlineGrid.Children.OfType<FrameworkElement>().Where(card => card.Visibility == Visibility.Visible).ToArray();
        HeadlineGrid.ColumnDefinitions.Clear();
        HeadlineGrid.RowDefinitions.Clear();
        for (var column = 0; column < columns; column++)
        {
            HeadlineGrid.ColumnDefinitions.Add(new ColumnDefinition());
        }

        for (var row = 0; row < (cards.Length + columns - 1) / columns; row++)
        {
            HeadlineGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (var index = 0; index < cards.Length; index++)
        {
            Grid.SetColumn(cards[index], index % columns);
            Grid.SetRow(cards[index], index / columns);
        }
    }

    private void UpdateLayoutColumns()
    {
        var wide = ActualWidth >= 960 && Model.HasPins;
        if (_wide == wide && _showPins == Model.HasPins)
        {
            return;
        }

        _wide = wide;
        _showPins = Model.HasPins;
        TelemetryGrid.ColumnDefinitions[0].Width = new GridLength(wide ? 1.55 : 1, GridUnitType.Star);
        TelemetryGrid.ColumnDefinitions[1].Width = wide ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        TelemetryGrid.ColumnSpacing = wide ? 14 : 0;
        Grid.SetRow(DetailsCard, wide || !Model.HasPins ? 0 : 1);
        Grid.SetColumn(DetailsCard, wide ? 1 : 0);
    }

    private void SetMetric(PinMetric metric) => _runtime.SetPinMetric(metric);

    private void OnModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(Model.HasFaults))
        {
            DetailsIcon.Tone = Model.HasFaults ? StatusTone.Negative : StatusTone.Neutral;
        }
        if (args.PropertyName == nameof(Model.HasPins))
        {
            UpdateLayoutColumns();
        }
    }

    private void RebuildMetrics(object? sender, NotifyCollectionChangedEventArgs? args)
    {
        MetricsHost.Children.Clear();
        foreach (var metric in Model.Metrics)
        {
            MetricsHost.Children.Add(new MetricTile(metric));
        }
    }

    private void RebuildFaults(object? sender, NotifyCollectionChangedEventArgs? args)
    {
        FaultsHost.Children.Clear();
        foreach (var fault in Model.Faults)
        {
            FaultsHost.Children.Add(new MetricTile(fault));
        }
        DetailsIcon.Tone = Model.HasFaults ? StatusTone.Negative : StatusTone.Neutral;
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        Model.PropertyChanged -= OnModelChanged;
        Model.Metrics.CollectionChanged -= RebuildMetrics;
        Model.Faults.CollectionChanged -= RebuildFaults;
    }

    private void Current_Click(object sender, RoutedEventArgs args) => SetMetric(PinMetric.Current);
    private void Power_Click(object sender, RoutedEventArgs args) => SetMetric(PinMetric.Power);
    private void Voltage_Click(object sender, RoutedEventArgs args) => SetMetric(PinMetric.Voltage);
    private void ResetPeak_Click(object sender, RoutedEventArgs args)
    {
        foreach (var pin in Model.Pins)
        {
            pin.ResetPeak();
        }
    }
    private async void Restart_Click(object sender, RoutedEventArgs args) => await _runtime.RestartPluginAsync(Model.Id);
    private async void ReadDevice_Click(object sender, RoutedEventArgs args) => await _runtime.OpenWireViewDeviceSettingsAsync();

    private async void ClearFaults_Click(object sender, RoutedEventArgs args)
    {
        var status = new CheckBox { Content = "Clear current fault-status flags", IsChecked = true };
        var log = new CheckBox { Content = "Clear logged fault flags", IsChecked = false };
        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(new TextBlock { Text = "Select which flags to clear. An ongoing physical fault can be reported again.", TextWrapping = TextWrapping.Wrap });
        stack.Children.Add(status);
        stack.Children.Add(log);
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Clear WireView faults",
            Content = stack,
            PrimaryButtonText = "Clear selected flags",
            CloseButtonText = "Cancel",
            RequestedTheme = ActualTheme
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await _runtime.ClearFaultsAsync(status.IsChecked == true, log.IsChecked == true);
        }
    }
}

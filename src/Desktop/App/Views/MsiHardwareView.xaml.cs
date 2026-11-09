using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rtx5090Monitoring.App.Controls;
using Rtx5090Monitoring.App.Services;
using Rtx5090Monitoring.Presentation.Telemetry;

namespace Rtx5090Monitoring.App.Views;

public sealed partial class MsiHardwareView : UserControl
{
    private readonly AppRuntime _runtime;
    private int _metricColumns;

    public HardwarePanelViewModel Model { get; }

    internal MsiHardwareView(HardwarePanelViewModel model, AppRuntime runtime)
    {
        Model = model;
        _runtime = runtime;
        InitializeComponent();

        foreach (var connector in Model.Msi.Connectors)
        {
            ConnectorGrid.Children.Add(new MsiConnectorCard(connector));
        }

        Loaded += (_, _) =>
        {
            _runtime.SafeguardHistoryChanged += UpdateSafeguardHistory;
            UpdateSafeguardHistory();
        };
        Unloaded += (_, _) => _runtime.SafeguardHistoryChanged -= UpdateSafeguardHistory;
        SizeChanged += (_, _) => LayoutPanels();
        LayoutPanels();
    }

    private void LayoutPanels()
    {
        LayoutStatusCards();

        var stackedHeader = ActualWidth < 960;
        Grid.SetRow(DeviceActions, stackedHeader ? 1 : 0);
        Grid.SetColumn(DeviceActions, stackedHeader ? 0 : 1);
        Grid.SetColumnSpan(DeviceActions, stackedHeader ? 2 : 1);

        var wide = ActualWidth >= 800;
        ConnectorGrid.ColumnDefinitions[1].Width = wide ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        Grid.SetColumn((FrameworkElement)ConnectorGrid.Children[1], wide ? 1 : 0);
        Grid.SetRow((FrameworkElement)ConnectorGrid.Children[1], wide ? 0 : 1);
    }

    private void LayoutStatusCards()
    {
        var columns = ActualWidth >= 1100 ? 4 : 2;

        if (_metricColumns == columns)
        {
            return;
        }

        _metricColumns = columns;

        MetricsGrid.ColumnDefinitions.Clear();

        for (var index = 0; index < columns; index++)
        {
            MetricsGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });
        }

        for (var index = 0; index < MetricsGrid.Children.Count; index++)
        {
            var card = (FrameworkElement)MetricsGrid.Children[index];
            Grid.SetRow(card, index / columns);
            Grid.SetColumn(card, index % columns);
        }
    }

    private void UpdateSafeguardHistory() => SafeguardHistoryButton.Visibility = _runtime.HasSafeguardHistory
        ? Visibility.Visible : Visibility.Collapsed;

    private void SafeguardHistory_Click(object sender, RoutedEventArgs args) => _runtime.OpenSafeguardHistory();

    private void ResetPeak_Click(object sender, RoutedEventArgs args) => Model.Msi.ResetPeaks();
    private async void Restart_Click(object sender, RoutedEventArgs args) => await _runtime.RestartPluginAsync(Model.Id);
}

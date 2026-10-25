using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rtx5090Monitoring.App.Controls;
using Rtx5090Monitoring.App.Services;
using Rtx5090Monitoring.Presentation.Telemetry;

namespace Rtx5090Monitoring.App.Views;

public sealed partial class MsiHardwareView : UserControl
{
    private readonly AppRuntime _runtime;
    public HardwarePanelViewModel Model { get; }

    internal MsiHardwareView(HardwarePanelViewModel model, AppRuntime runtime)
    {
        Model = model;
        _runtime = runtime;
        InitializeComponent();
        var metrics = new[] { Model.Msi.Temperature, Model.Msi.FanSpeed, Model.Msi.FanMode, Model.Msi.FanDuty };
        for (var index = 0; index < metrics.Length; index++)
        {
            var tile = new MetricTile(metrics[index]);
            Grid.SetRow(tile, index / 2);
            Grid.SetColumn(tile, index % 2);
            MetricsGrid.Children.Add(tile);
        }
        foreach (var connector in Model.Msi.Connectors)
        {
            ConnectorGrid.Children.Add(new MsiConnectorCard(connector));
        }

        SizeChanged += (_, _) => LayoutConnectors();
        LayoutConnectors();
    }

    private void LayoutConnectors()
    {
        var wide = ActualWidth >= 800;
        ConnectorGrid.ColumnDefinitions[1].Width = wide ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        Grid.SetColumn((FrameworkElement)ConnectorGrid.Children[1], wide ? 1 : 0);
        Grid.SetRow((FrameworkElement)ConnectorGrid.Children[1], wide ? 0 : 1);
    }

    private void ResetPeak_Click(object sender, RoutedEventArgs args) => Model.Msi.ResetPeaks();
    private async void Restart_Click(object sender, RoutedEventArgs args) => await _runtime.RestartPluginAsync(Model.Id);
}

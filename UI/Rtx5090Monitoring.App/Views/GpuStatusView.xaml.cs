using System.Collections.Specialized;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rtx5090Monitoring.App.Controls;
using Rtx5090Monitoring.Presentation.Telemetry;

namespace Rtx5090Monitoring.App.Views;

public sealed partial class GpuStatusView : UserControl
{
    public GpuStatusViewModel Model { get; }
    public bool ShowSummary { get; }

    internal GpuStatusView(GpuStatusViewModel model, bool showSummary)
    {
        Model = model;
        ShowSummary = showSummary;
        InitializeComponent();

        if (showSummary)
        {
            StatusFrame.Style = (Style)Application.Current.Resources["MonitorCard"];
            StatusFrame.HorizontalAlignment = HorizontalAlignment.Stretch;
            StatusFrame.VerticalAlignment = VerticalAlignment.Top;
        }

        RebuildMetrics(null, null);
        Loaded += (_, _) =>
        {
            if (ShowSummary)
            {
                Model.DetailedMetrics.CollectionChanged += RebuildMetrics;
                RebuildMetrics(null, null);
            }
        };
        Unloaded += (_, _) => Model.DetailedMetrics.CollectionChanged -= RebuildMetrics;
    }

    private void RebuildMetrics(object? sender, NotifyCollectionChangedEventArgs? args)
    {
        MetricsHost.Children.Clear();
        foreach (var metric in ShowSummary ? Model.DetailedMetrics : Model.Metrics)
        {
            MetricsHost.Children.Add(new MetricTile(metric, valueMaxWidth: 260));
        }
    }
}

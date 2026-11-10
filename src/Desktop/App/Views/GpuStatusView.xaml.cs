using System.Collections.Specialized;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RTXLens.App.Controls;
using RTXLens.Presentation.Telemetry;

namespace RTXLens.App.Views;

public sealed partial class GpuStatusView : UserControl
{
    public GpuStatusViewModel Model { get; }
    public bool ShowSummary { get; }

    private readonly IReadOnlyList<MetricViewModel> _additionalMetrics;

    internal GpuStatusView(GpuStatusViewModel model, bool showSummary, IReadOnlyList<MetricViewModel> additionalMetrics)
    {
        Model = model;
        ShowSummary = showSummary;
        _additionalMetrics = additionalMetrics;
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
        var metrics = ShowSummary ? Model.DetailedMetrics : Model.Metrics;

        foreach (var metric in metrics.Concat(_additionalMetrics))
        {
            MetricsHost.Children.Add(new MetricTile(metric, valueMaxWidth: 260, usePanelBorder: true));
        }
    }
}

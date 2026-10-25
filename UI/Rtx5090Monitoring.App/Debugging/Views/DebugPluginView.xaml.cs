using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rtx5090Monitoring.Debugging.ViewModels;

namespace Rtx5090Monitoring.App.Debugging.Views;

internal sealed partial class DebugPluginView : UserControl
{
    private readonly Action<string> _reset;
    public DebugPluginViewModel Model { get; }
    internal DebugPluginView(DebugPluginViewModel model, Action<string> reset)
    {
        Model = model;
        _reset = reset;
        InitializeComponent();
        foreach (var group in model.Metrics.GroupBy(metric => metric.Definition.Group))
        {
            var rows = new StackPanel { Spacing = 12 };
            foreach (var metric in group)
            {
                rows.Children.Add(new DebugMetricEditor(metric));
            }

            MetricGroups.Children.Add(new Expander
            {
                Header = group.Key, Content = rows, HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch, IsExpanded = group.Key is "Connector" or "GPU" or "PSU" or "PSU connector 1"
            });
        }
    }
    private void Reset_Click(object sender, RoutedEventArgs args) => _reset(Model.Id);
}

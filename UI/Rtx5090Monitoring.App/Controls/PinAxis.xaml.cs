using System.ComponentModel;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Rtx5090Monitoring.Presentation.Telemetry;

namespace Rtx5090Monitoring.App.Controls;

public sealed partial class PinAxis : UserControl
{
    private readonly PinViewModel _model;
    private PinChartScale? _scale;

    public PinAxis(PinViewModel model)
    {
        _model = model;
        InitializeComponent();
        model.PropertyChanged += OnModelChanged;
        ActualThemeChanged += (_, _) => UpdateAxis(true);
        Unloaded += (_, _) => model.PropertyChanged -= OnModelChanged;
        UpdateAxis();
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs args) => UpdateAxis();

    private void UpdateAxis(bool force = false)
    {
        HeadroomSpacer.Visibility = _model.ShowHeadroom ? Visibility.Visible : Visibility.Collapsed;
        if (!force && _scale == _model.Scale)
        {
            return;
        }

        _scale = _model.Scale;
        var brush = Foreground;
        Ticks.Children.Clear();
        Ticks.Children.Add(new Rectangle { Width = 1, Height = 158, Fill = brush });
        Canvas.SetLeft(Ticks.Children[0], 43);
        for (var index = 0; index <= _scale.Intervals; index++)
        {
            var value = _scale.Minimum + (_scale.Maximum - _scale.Minimum) * index / _scale.Intervals;
            var top = (1 - _scale.Position(value)) * 158;
            var label = new TextBlock
            {
                Text = value.ToString("0.##", CultureInfo.CurrentCulture) + _scale.Unit,
                Width = 38, FontSize = 10, TextAlignment = TextAlignment.Right, Foreground = brush
            };
            Canvas.SetTop(label, top - 7);
            Ticks.Children.Add(label);
            var tick = new Rectangle { Width = 5, Height = 1, Fill = brush };
            Canvas.SetTop(tick, top);
            Canvas.SetLeft(tick, 43);
            Ticks.Children.Add(tick);
        }
    }
}

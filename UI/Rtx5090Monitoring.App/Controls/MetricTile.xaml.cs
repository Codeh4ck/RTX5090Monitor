using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rtx5090Monitoring.Presentation.Telemetry;

namespace Rtx5090Monitoring.App.Controls;

public sealed partial class MetricTile : UserControl
{
    public MetricViewModel Model { get; }
    public bool ShowIcon { get; }
    public string DisplayLabel => Model.Label switch { "Core temperature" => "Core temp", "VRAM temperature" => "VRAM temp", _ => Model.Label };

    public MetricTile(MetricViewModel model, bool showIcon = true)
    {
        Model = model;
        ShowIcon = showIcon;
        InitializeComponent();
        if (!ShowIcon)
        {
            Grid.SetColumn(MetricLabel, 0);
            Grid.SetColumnSpan(MetricLabel, 2);
        }
        Loaded += (_, _) =>
        {
            Model.PropertyChanged += OnModelChanged;
            UpdateTone();
        };
        Unloaded += (_, _) => Model.PropertyChanged -= OnModelChanged;
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(Model.Tone))
        {
            UpdateTone();
        }
    }

    private void UpdateTone()
    {
        if (ShowIcon)
        {
            MetricIcon.Tone = Model.Tone;
        }
        VisualStateManager.GoToState(this, Model.Tone.ToString(), false);
    }
}

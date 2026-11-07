using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rtx5090Monitoring.Presentation.Telemetry;

namespace Rtx5090Monitoring.App.Controls;

public sealed partial class PinBar : UserControl, INotifyPropertyChanged
{
    public PinViewModel Model { get; }
    internal bool IsPsuCurrentVisible => PsuCurrentBorder.Visibility == Visibility.Visible;
    public Visibility PeakVisibility => Model.ShowPeak ? Visibility.Visible : Visibility.Collapsed;
    public Visibility HeadroomVisibility => Model.ShowHeadroom ? Visibility.Visible : Visibility.Collapsed;
    public event PropertyChangedEventHandler? PropertyChanged;

    public PinBar(PinViewModel model)
    {
        Model = model;
        InitializeComponent();
        Model.PropertyChanged += OnModelChanged;
        Unloaded += OnUnloaded;
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs args) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    private void OnUnloaded(object sender, RoutedEventArgs args) => Model.PropertyChanged -= OnModelChanged;
}

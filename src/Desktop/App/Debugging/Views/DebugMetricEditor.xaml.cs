using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rtx5090Monitoring.Debugging.ViewModels;

namespace Rtx5090Monitoring.App.Debugging.Views;

internal sealed partial class DebugMetricEditor : UserControl
{
    private bool _syncingFaults;
    public DebugMetricViewModel Model { get; }
    public string Caption => Model.Definition.Label + " · " + Model.Definition.Unit;
    internal DebugMetricEditor(DebugMetricViewModel model)
    {
        Model = model;
        InitializeComponent();
        ValueInput.Visibility = model.Definition.IsBoolean ? Visibility.Collapsed : Visibility.Visible;
        BooleanInput.Visibility = model.Definition.IsBoolean ? Visibility.Visible : Visibility.Collapsed;
        FaultBitsButton.Visibility = model.Definition.IsFault ? Visibility.Visible : Visibility.Collapsed;
        ValueInput.NumberFormatter = new global::Windows.Globalization.NumberFormatting.DecimalFormatter { FractionDigits = 0 };
        if (!model.Definition.IsFault)
        {
            return;
        }

        var menu = new MenuFlyout();
        foreach (var (mask, label) in new[] { (2, "Overtemperature"), (4, "Overcurrent"), (8, "Wire overcurrent"), (16, "Overpower"), (32, "Current imbalance") })
        {
            var item = new ToggleMenuFlyoutItem { Text = label, Tag = mask };
            item.Click += (_, _) => SetFault(mask, item.IsChecked);
            menu.Items.Add(item);
        }
        menu.Opening += (_, _) =>
        {
            _syncingFaults = true;
            foreach (var item in menu.Items.OfType<ToggleMenuFlyoutItem>())
            {
                item.IsChecked = ((ushort)Model.Value & (int)item.Tag) != 0;
            }

            _syncingFaults = false;
        };
        FaultBitsButton.Flyout = menu;
    }
    private void SetFault(int mask, bool enabled)
    {
        if (!_syncingFaults)
        {
            Model.Value = enabled ? (ushort)Model.Value | mask : (ushort)Model.Value & ~mask;
        }
    }
    private void Reset_Click(object sender, RoutedEventArgs args) => Model.Reset();
}

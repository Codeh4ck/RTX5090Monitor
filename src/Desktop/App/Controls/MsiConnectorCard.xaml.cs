using Microsoft.UI.Xaml.Controls;
using Rtx5090Monitoring.Presentation.Telemetry;

namespace Rtx5090Monitoring.App.Controls;

public sealed partial class MsiConnectorCard : UserControl
{
    public MsiConnectorViewModel Model { get; }

    public MsiConnectorCard(MsiConnectorViewModel model)
    {
        Model = model;
        InitializeComponent();
        PinHost.Children.Add(new PinAxis(Model.Pins[0]));
        foreach (var pin in Model.Pins)
        {
            var bar = new PinBar(pin);
            Grid.SetColumn(bar, pin.Channel);
            PinHost.Children.Add(bar);
        }
    }
}

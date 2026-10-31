using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Rtx5090Monitoring.Presentation.Configuration;

namespace Rtx5090Monitoring.App.Views;

public sealed partial class ConnectorProfilesView : UserControl
{
    public ConnectorProfilesEditorViewModel Model { get; }

    internal ConnectorProfilesView(ConnectorProfilesEditorViewModel model)
    {
        Model = model;
        InitializeComponent();
        Rebuild();
    }

    private void Profile_Changed(object sender, SelectionChangedEventArgs args)
    {
        if (sender is ComboBox { SelectedItem: ConnectorProfileEditorViewModel profile })
        {
            Model.SelectedProfile = profile;
            Rebuild();
        }
    }

    private void Rebuild()
    {
        if (ProfileHost is null)
        {
            return;
        }

        ProfileHost.Children.Clear();
        if (Model.SelectedProfile is not { } profile)
        {
            return;
        }

        if (profile.HasMsi)
        {
            var connector = new ComboBox { Header = "MSI connector feeding this GPU", HorizontalAlignment = HorizontalAlignment.Stretch, DataContext = profile };
            connector.Items.Add("Automatic - nonzero pins (one active connector)");
            connector.Items.Add("Connector 1");
            connector.Items.Add("Connector 2");
            connector.SetBinding(Selector.SelectedIndexProperty, new Binding { Path = new PropertyPath(nameof(profile.MsiConnectorIndex)), Mode = BindingMode.TwoWay });
            ProfileHost.Children.Add(connector);
            ProfileHost.Children.Add(new TextBlock
            {
                Text = "If both connectors carry current, choose the one feeding this GPU. At idle, Automatic keeps its last known connector. MSI supplies current only.",
                Style = (Style)Application.Current.Resources["MonitorCaption"]
            });
        }

        foreach (var metric in profile.Metrics)
        {
            ProfileHost.Children.Add(new ConnectorMetricEditorView(metric));
        }
    }
}

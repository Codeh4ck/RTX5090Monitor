using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace Rtx5090Monitoring.App.Controls;

[ContentProperty(Name = nameof(Body))]
public sealed partial class SectionPanel : UserControl
{
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(nameof(Header), typeof(string), typeof(SectionPanel), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty HeaderActionsProperty = DependencyProperty.Register(nameof(HeaderActions), typeof(UIElement), typeof(SectionPanel), new PropertyMetadata(null));
    public static readonly DependencyProperty BodyProperty = DependencyProperty.Register(nameof(Body), typeof(UIElement), typeof(SectionPanel), new PropertyMetadata(null));

    public string Header { get => (string)GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    public UIElement? HeaderActions { get => (UIElement?)GetValue(HeaderActionsProperty); set => SetValue(HeaderActionsProperty, value); }
    public UIElement? Body { get => (UIElement?)GetValue(BodyProperty); set => SetValue(BodyProperty, value); }

    public SectionPanel()
    {
        InitializeComponent();
    }
}

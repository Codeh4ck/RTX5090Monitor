using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RTXLens.Presentation.Telemetry;

namespace RTXLens.App.Controls;

public sealed partial class WireViewHeadlineCard : UserControl
{
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(
        nameof(Tone), typeof(StatusTone), typeof(WireViewHeadlineCard),
        new PropertyMetadata(StatusTone.Neutral, OnToneChanged));

    public StatusTone Tone
    {
        get => (StatusTone)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    public WireViewHeadlineCard()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateTone();
    }

    private static void OnToneChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((WireViewHeadlineCard)sender).UpdateTone();

    private void UpdateTone() =>
        VisualStateManager.GoToState(this, Tone == StatusTone.Negative ? "Fault" : "Normal", false);
}


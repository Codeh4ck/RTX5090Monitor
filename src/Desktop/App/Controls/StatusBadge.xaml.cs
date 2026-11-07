using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rtx5090Monitoring.Presentation.Telemetry;

namespace Rtx5090Monitoring.App.Controls;

public sealed partial class StatusBadge : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(StatusBadge), new PropertyMetadata(string.Empty, OnBadgeChanged));
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(nameof(Tone), typeof(StatusTone), typeof(StatusBadge), new PropertyMetadata(StatusTone.Neutral, OnBadgeChanged));
    public static readonly DependencyProperty ShowIndicatorProperty = DependencyProperty.Register(nameof(ShowIndicator), typeof(bool), typeof(StatusBadge), new PropertyMetadata(true, OnBadgeChanged));
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public StatusTone Tone { get => (StatusTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }
    public bool ShowIndicator { get => (bool)GetValue(ShowIndicatorProperty); set => SetValue(ShowIndicatorProperty, value); }

    public StatusBadge()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateBadge();
    }

    private static void OnBadgeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((StatusBadge)sender).UpdateBadge();

    private void UpdateBadge()
    {
        Label.Text = Text;
        Dot.Visibility = ShowIndicator ? Visibility.Visible : Visibility.Collapsed;
        VisualStateManager.GoToState(this, Tone.ToString(), false);
    }
}

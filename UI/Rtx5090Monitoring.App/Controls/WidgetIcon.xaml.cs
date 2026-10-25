using Rtx5090Monitoring.Presentation.Telemetry;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Rtx5090Monitoring.App.Controls;

public sealed partial class WidgetIcon : UserControl
{
    private static readonly Dictionary<string, SvgImageSource> Sources = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> Failures = new(StringComparer.Ordinal);
    internal static IReadOnlyDictionary<string, string> LoadFailures => Failures;
    public static readonly DependencyProperty IconNameProperty = DependencyProperty.Register(nameof(IconName), typeof(string), typeof(WidgetIcon),
        new PropertyMetadata("vcore", OnIconChanged));

    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(nameof(Tone), typeof(StatusTone), typeof(WidgetIcon),
        new PropertyMetadata(StatusTone.Neutral, OnToneChanged));
    public StatusTone Tone { get => (StatusTone)GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    public string IconName { get => (string)GetValue(IconNameProperty); set => SetValue(IconNameProperty, value); }

    public WidgetIcon()
    {
        InitializeComponent();
        GlyphImage.ImageFailed += (_, args) => Failures[IconName] = args.ErrorMessage;
        LoadIcon();
        Loaded += (_, _) => UpdateTone();
    }

    private static void OnIconChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((WidgetIcon)sender).LoadIcon();

    private static void OnToneChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((WidgetIcon)sender).UpdateTone();
    private void UpdateTone() => VisualStateManager.GoToState(this, Tone == StatusTone.Negative ? "Fault" : "Normal", false);

    private void LoadIcon()
    {
        if (!Sources.TryGetValue(IconName, out var source))
        {
            source = new SvgImageSource(new Uri($"ms-appx:///Assets/Icons/{IconName}.svg")) { RasterizePixelWidth = 96, RasterizePixelHeight = 96 };
            Sources.Add(IconName, source);
        }

        GlyphImage.Source = source;
    }
}

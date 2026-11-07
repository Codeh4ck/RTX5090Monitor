using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Rtx5090Monitoring.Presentation.Settings;

namespace Rtx5090Monitoring.App.Services;

internal static class ThemeService
{
    public static void Apply(FrameworkElement root, AppPreferences preferences)
    {
        root.RequestedTheme = preferences.Theme switch { AppTheme.Light => ElementTheme.Light, AppTheme.Dark => ElementTheme.Dark, _ => ElementTheme.Default };
        var color = Windows.UI.Color.FromArgb(255, Convert.ToByte(preferences.AccentColor[1..3], 16),
            Convert.ToByte(preferences.AccentColor[3..5], 16), Convert.ToByte(preferences.AccentColor[5..7], 16));
        foreach (var dictionary in Application.Current.Resources.MergedDictionaries)
        {
            foreach (var name in new[] { "Light", "Dark" })
            {
                if (!dictionary.ThemeDictionaries.TryGetValue(name, out var value) || value is not ResourceDictionary theme)
                {
                    continue;
                }

                var adjusted = color;
                if (name == "Light" && (color.R * 0.2126 + color.G * 0.7152 + color.B * 0.0722) > 150)
                {
                    adjusted = Windows.UI.Color.FromArgb(255, (byte)(color.R * 0.55), (byte)(color.G * 0.55), (byte)(color.B * 0.55));
                }

                if (name == "Dark" && Math.Max(color.R, Math.Max(color.G, color.B)) < 90)
                {
                    adjusted = Windows.UI.Color.FromArgb(255, (byte)Math.Max(100, (int)color.R), (byte)Math.Max(100, (int)color.G), (byte)Math.Max(100, (int)color.B));
                }

                if (theme.TryGetValue("MonitorAccent", out var accent) && accent is SolidColorBrush brush)
                {
                    brush.Color = adjusted;
                }
            }
        }
        if (root is Control control)
        {
            control.FontSize = 14 * preferences.TextScale;
        }

        foreach (var name in new[] { "ControlContentThemeFontSize", "TextControlThemeFontSize", "BodyTextBlockFontSize" })
        {
            root.Resources[name] = 14 * preferences.TextScale;
        }
    }
}

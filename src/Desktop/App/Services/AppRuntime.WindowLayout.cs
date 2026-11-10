using Microsoft.UI.Xaml;
using RTXLens.App.Platform.Windowing;
using RTXLens.Presentation.Settings;
using Windows.Foundation;

namespace RTXLens.App.Services;

internal sealed partial class AppRuntime
{
    private readonly Dictionary<Window, (string Key, NativeWindowLayout Layout)> _windowLayouts = [];

    internal void TrackWindow(Window window, string key, double width, double height,
        bool center = true, Window? owner = null, bool besideOwner = false)
    {
        var saved = Preferences.WindowPlacements?.GetValueOrDefault(key);
        if (key == "main" && saved?.Width is null && saved?.Height is null)
        {
            const int previousDefaultWidth = 1100;
            const int previousDefaultHeight = 1050;
            var legacyWidth = Preferences.WindowWidth is previousDefaultWidth or AppPreferences.DefaultWindowWidth ? width : Preferences.WindowWidth;
            var legacyHeight = Preferences.WindowHeight is previousDefaultHeight or AppPreferences.DefaultWindowHeight ? height : Preferences.WindowHeight;
            if (legacyWidth != width || legacyHeight != height)
            {
                saved = (saved ?? new WindowPlacement()) with { Width = legacyWidth, Height = legacyHeight };
            }
        }

        var layout = new NativeWindowLayout(window, () => Preferences, saved, new Size(width, height), center, besideOwner, owner ?? _window);
        _windowLayouts.Add(window, (key, layout));
        window.Closed += (_, _) =>
        {
            if (_windowLayouts.Remove(window, out var tracked))
            {
                SaveWindowPlacement(tracked.Key, tracked.Layout);
            }
        };
    }

    internal void SaveWindowPlacement(Window window)
    {
        if (_windowLayouts.TryGetValue(window, out var tracked))
        {
            SaveWindowPlacement(tracked.Key, tracked.Layout);
        }
    }

    private void SaveWindowPlacements()
    {
        foreach (var (key, layout) in _windowLayouts.Values)
        {
            SaveWindowPlacement(key, layout);
        }
    }

    private void SaveWindowPlacement(string key, NativeWindowLayout layout)
    {
        var previous = Preferences.WindowPlacements?.GetValueOrDefault(key);
        var placement = layout.Capture(previous);
        if (placement != (previous ?? new WindowPlacement()))
        {
            Settings.SaveWindowPlacement(key, placement);
        }
    }
}


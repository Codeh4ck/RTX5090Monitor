using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using RTXLens.App.Platform.Interop;
using RTXLens.Presentation.Settings;
using Windows.Foundation;
using Windows.Graphics;

namespace RTXLens.App.Platform.Windowing;

internal sealed class NativeWindowLayout
{
    private readonly Window _window;
    private readonly AppWindow _native;
    private readonly Func<AppPreferences> _preferences;
    private Size? _manualSize;
    private SizeInt32? _initialSize;
    private PointInt32? _position;
    private bool _wasRestored = true;
    private double _previousScale;

    private bool IsRestored => _native.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Restored };
    private double Scale => Math.Max(96, NativeMethods.GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(_window))) / 96d;

    internal NativeWindowLayout(Window window, Func<AppPreferences> preferences, WindowPlacement? saved,
        Size defaultSize, bool centerOnOwner, bool besideOwner, Window? owner)
    {
        _window = window;
        _native = window.AppWindow;
        _preferences = preferences;

        var settings = preferences();
        var ownerArea = DisplayArea.GetFromWindowId((owner ?? window).AppWindow.Id, DisplayAreaFallback.Primary);
        var initial = _native.Position;
        if (settings.RememberWindowPosition && saved is { X: { } x, Y: { } y })
        {
            initial = new PointInt32(x, y);
            ownerArea = DisplayArea.GetFromPoint(initial, DisplayAreaFallback.Nearest);
        }

        var area = ownerArea.WorkArea;
        _native.Move(ClampPosition(initial, new SizeInt32(1, 1), area));
        var scale = Scale;
        if (settings.RememberWindowSize && saved is { Width: { } width, Height: { } height })
        {
            _manualSize = new Size(width, height);
        }

        var desired = _manualSize ?? defaultSize;
        var pixels = new SizeInt32(Math.Min(area.Width, (int)Math.Round(desired.Width * scale)),
            Math.Min(area.Height, (int)Math.Round(desired.Height * scale)));

        if (centerOnOwner && (!settings.RememberWindowPosition || saved?.X is null))
        {
            if (besideOwner && owner is not null)
            {
                var ownerPosition = owner.AppWindow.Position;
                var right = ownerPosition.X + owner.AppWindow.Size.Width + 8;
                var left = ownerPosition.X - pixels.Width - 8;
                var side = right + pixels.Width <= area.X + area.Width ? right : left;
                initial = new PointInt32(side, ownerPosition.Y);
            }
            else
            {
                initial = new PointInt32(area.X + (area.Width - pixels.Width) / 2, area.Y + (area.Height - pixels.Height) / 2);
            }
        }

        var position = ClampPosition(initial, pixels, area);
        _native.MoveAndResize(new RectInt32(position.X, position.Y, pixels.Width, pixels.Height));
        _position = _native.Position;
        _initialSize = _native.Size;
        _previousScale = scale;

        _native.Changed += OnWindowChanged;
        window.Closed += (_, _) => _native.Changed -= OnWindowChanged;
    }

    internal WindowPlacement Capture(WindowPlacement? previous)
    {
        var settings = _preferences();
        var saved = previous ?? new WindowPlacement();
        if (settings.RememberWindowSize && _manualSize is { } size)
        {
            saved = saved with { Width = size.Width, Height = size.Height };
        }

        if (settings.RememberWindowPosition && _position is { } position)
        {
            saved = saved with { X = position.X, Y = position.Y };
        }

        return saved;
    }

    private void OnWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        var restored = IsRestored;
        if (restored)
        {
            if (args.DidPositionChange)
            {
                _position = sender.Position;
            }

            var scale = Scale;
            if (args.DidSizeChange && sender.Size != _initialSize)
            {
                if (_wasRestored && scale == _previousScale)
                {
                    _manualSize = new Size(Math.Clamp(sender.Size.Width / scale, 1, WindowSizeLimits.MaximumWidth),
                        Math.Clamp(sender.Size.Height / scale, 1, WindowSizeLimits.MaximumHeight));
                }

                _initialSize = null;
            }

            _previousScale = scale;
        }

        _wasRestored = restored;
    }

    private static PointInt32 ClampPosition(PointInt32 position, SizeInt32 size, RectInt32 area) =>
        new(Math.Clamp(position.X, area.X, area.X + Math.Max(0, area.Width - size.Width)),
            Math.Clamp(position.Y, area.Y, area.Y + Math.Max(0, area.Height - size.Height)));
}


using Microsoft.UI.Xaml;
using Rtx5090Monitoring.App.Platform.Startup;
using Rtx5090Monitoring.App.Platform.Tray;
using Rtx5090Monitoring.App.Platform.Windowing;
using Rtx5090Monitoring.App.Services;
using Windows.Graphics;

namespace Rtx5090Monitoring.App;

public sealed partial class App : Application
{
    private SingleInstance? _instance;
    private MainWindow? _window;
    private AppRuntime? _runtime;
    private TrayService? _tray;
    private bool _exiting;
    private bool _hidden;

    public App()
    {
        UnhandledException += (_, error) => File.WriteAllText(Path.Combine(Path.GetTempPath(), "rtx-monitor-error.txt"), error.Exception.ToString());
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var arguments = Environment.GetCommandLineArgs();
#if TELEMETRY_DEBUG
        var demo = arguments.Contains("--demo");
        var diagnostic = demo && arguments.Contains("--smoke-test");
#else
        var demo = false;
        var diagnostic = false;
#endif
        _instance = new SingleInstance(diagnostic);
        if (!_instance.IsFirst)
        {
            SingleInstance.ShowExisting();
            _instance.Dispose();
            Exit();
            return;
        }

        _runtime = new AppRuntime(demo, Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread(), diagnostic);
        _window = new MainWindow(_runtime);
        _window.AppWindow.Resize(new SizeInt32((int)Math.Min(_runtime.Preferences.WindowWidth, WindowSizeLimits.MaximumWidth),
            (int)Math.Min(_runtime.Preferences.WindowHeight, WindowSizeLimits.MaximumHeight)));
        _window.AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "monitor.ico"));
        _window.AppWindow.Closing += (_, closing) =>
        {
            if (_exiting)
            {
                return;
            }

            closing.Cancel = true;
            if (_runtime.Preferences.CloseToTray && _tray?.IsAvailable == true)
            {
                Hide();
            }
            else
            {
                ExitApplication();
            }
        };
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(_window);
        _tray = new TrayService(handle, _window.DispatcherQueue, Show, () => { Show(); _window.Navigate("settings"); },
            ExitApplication, minimized =>
            {
                if (minimized)
                {
                    _runtime.SetRendering(false);
                    if (_runtime.Preferences.MinimizeToTray && _tray?.IsAvailable == true)
                    {
                        Hide();
                    }
                }
                else if (!_hidden)
                {
                    _runtime.SetRendering(true);
                }
            });
        _window.Activate();
        if (_window.ThemeRoot.XamlRoot is null)
        {
            var loaded = new TaskCompletionSource();
            void OnLoaded(object sender, RoutedEventArgs eventArgs)
            {
                _window.ThemeRoot.Loaded -= OnLoaded;
                loaded.TrySetResult();
            }
            _window.ThemeRoot.Loaded += OnLoaded;
            await loaded.Task;
        }
        var scale = (_window.ThemeRoot.XamlRoot ?? throw new InvalidOperationException("Window content has not loaded.")).RasterizationScale;
        var area = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(_window.AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Primary).WorkArea;
        var width = diagnostic ? WindowSizeLimits.MaximumWidth : Math.Min(_runtime.Preferences.WindowWidth, WindowSizeLimits.MaximumWidth);
        var height = diagnostic ? WindowSizeLimits.MaximumHeight : Math.Min(_runtime.Preferences.WindowHeight, WindowSizeLimits.MaximumHeight);
        _window.AppWindow.Resize(new SizeInt32(Math.Min(area.Width, (int)(width * scale)), Math.Min(area.Height, (int)(height * scale))));
        await _runtime.StartAsync(_window, _tray);
#if TELEMETRY_DEBUG
        if (arguments.Contains("--smoke-test") && _runtime.IsDemo)
        {
            var output = arguments.FirstOrDefault(argument => argument.StartsWith("--smoke-output=", StringComparison.Ordinal))?[15..]
                ?? Path.Combine(Path.GetTempPath(), "rtx-monitor-smoke");
            try { await Diagnostics.SmokeTestRunner.RunAsync(_window, _runtime, output); }
            catch (Exception exception)
            {
                Directory.CreateDirectory(output);
                await File.WriteAllTextAsync(Path.Combine(output, "smoke-error.txt"), exception.ToString());
                Environment.ExitCode = 1;
            }
            ExitApplication();
            return;
        }
#endif
        if (_runtime.Preferences.StartMinimized || arguments.Contains("--minimized"))
        {
            if (_tray.IsAvailable)
            {
                Hide();
            }
            else
            {
                Platform.Interop.NativeMethods.ShowWindow(handle, Platform.Interop.NativeConstants.MinimizeWindow);
            }
        }
    }

    private void Show()
    {
        if (_window is null || _runtime is null)
        {
            return;
        }

        _hidden = false;
        _window.AppWindow.Show();
        Platform.Interop.NativeMethods.ShowWindow(WinRT.Interop.WindowNative.GetWindowHandle(_window), Platform.Interop.NativeConstants.RestoreWindow);
        _runtime.SetRendering(true);
        _window.Activate();
    }

    private void Hide()
    {
        _hidden = true;
        _runtime?.SetRendering(false);
        _window?.AppWindow.Hide();
    }

    private async void ExitApplication()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        if (_runtime is not null && _window is not null)
        {
            var size = _window.AppWindow.Size;
            var scale = Math.Max(96, Platform.Interop.NativeMethods.GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(_window))) / 96d;
            _runtime.Settings.SaveWindowSize(Math.Clamp(size.Width / scale, WindowSizeLimits.MinimumWidth, WindowSizeLimits.MaximumWidth),
                Math.Clamp(size.Height / scale, WindowSizeLimits.MinimumHeight, WindowSizeLimits.MaximumHeight));
            try { await _runtime.StopAsync(); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { System.Diagnostics.Debug.WriteLine(exception); }
        }
        _tray?.Dispose();
        _instance?.Dispose();
        _window?.Close();
        Exit();
    }
}

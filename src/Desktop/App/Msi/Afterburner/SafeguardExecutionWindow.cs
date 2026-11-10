using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using RTXLens.App.Services;
using RTXLens.Presentation.Settings;
using RTXLens.SafeguardReporting;
using Windows.Graphics;

namespace RTXLens.App.Msi.Afterburner;

internal sealed class SafeguardExecutionWindow : Window
{
    private readonly WebView2 _browser = new();
    private readonly ScrollViewer _fallback;
    private Task? _initialization;
    private bool _closed;

    public Guid ExecutionId { get; }

    public SafeguardExecutionWindow(SafeguardReport report, AppPreferences preferences, double scale)
    {
        ExecutionId = report.Id;
        Title = "RTX Lens - Safeguard+ execution";
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "monitor.ico"));
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        AppWindow.Resize(new SizeInt32(Math.Min(area.Width, (int)(820 * scale)), Math.Min(area.Height, (int)(780 * scale))));

        var (html, plain) = SafeguardReportRenderer.Render(report);
        _fallback = new ScrollViewer
        {
            Visibility = Visibility.Collapsed,
            Content = new TextBlock { Text = plain, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true, Margin = new Thickness(16) }
        };
        var root = new Grid();
        root.Children.Add(_browser);
        root.Children.Add(_fallback);
        Content = root;
        ThemeService.Apply(root, preferences);

        root.Loaded += (_, _) => _initialization ??= InitializeAsync(html);
        Closed += async (_, _) =>
        {
            _closed = true;
            if (_initialization is not null)
            {
                await _initialization;
            }

            _browser.Close();
        };
    }

    private async Task InitializeAsync(string html)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RTXLens", "SafeguardReport.WebView2");
            var environment = await CoreWebView2Environment.CreateWithOptionsAsync(null, folder, new CoreWebView2EnvironmentOptions());
            if (_closed)
            {
                return;
            }

            await _browser.EnsureCoreWebView2Async(environment);
            if (_closed)
            {
                return;
            }

            var core = _browser.CoreWebView2;
            core.Settings.IsScriptEnabled = false;
            core.Settings.IsWebMessageEnabled = false;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.AreDevToolsEnabled = false;
            core.NewWindowRequested += (_, args) => args.Handled = true;
            core.NavigationStarting += (_, args) =>
                args.Cancel = args.Uri != "about:blank" && !args.Uri.StartsWith("data:text/html;", StringComparison.OrdinalIgnoreCase);
            core.NavigationCompleted += (_, args) =>
            {
                if (!args.IsSuccess)
                {
                    ShowFallback();
                }
            };
            _browser.NavigateToString(html);
        }
        catch (Exception error) when (error is COMException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            ShowFallback();
        }
    }

    private void ShowFallback()
    {
        if (_closed)
        {
            return;
        }

        _browser.Visibility = Visibility.Collapsed;
        _fallback.Visibility = Visibility.Visible;
    }
}

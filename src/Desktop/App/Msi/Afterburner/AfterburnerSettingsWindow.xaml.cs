using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rtx5090Monitoring.App.Services;
using Rtx5090Monitoring.Presentation.Settings;

namespace Rtx5090Monitoring.App.Msi.Afterburner;

internal sealed partial class AfterburnerSettingsWindow : Window
{
    private bool _closeApproved;
    private bool _confirmingClose;
    private bool _initialHeightFitted;
    internal AfterburnerSettingsView Integration { get; }
    internal FrameworkElement ThemeRoot => Root;

    internal AfterburnerSettingsWindow(AppRuntime runtime)
    {
        InitializeComponent();
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "monitor.ico"));
        Integration = new AfterburnerSettingsView(runtime);
        Integration.CloseRequested += Close;
        IntegrationHost.Content = Integration;
        ApplyAppearance(runtime.Preferences);
        Integration.Loaded += (_, _) => DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, FitInitialHeight);
        AppWindow.Closing += OnClosing;
        Closed += (_, _) =>
        {
            Integration.StopMonitoring();
#if DEBUG || TELEMETRY_DEBUG
            AfterburnerDiffWindow.CurrentPreview?.CancelPreview();
#endif
        };
    }

    private void FitInitialHeight()
    {
        if (_initialHeightFitted || !Root.IsLoaded || Root.XamlRoot is not { } xamlRoot)
        {
            return;
        }

        Root.UpdateLayout();
        if (Integration.PreferredContentHeight <= 0 || Root.ActualHeight <= 0)
        {
            return;
        }

        _initialHeightFitted = true;
        var scale = xamlRoot.RasterizationScale;
        var frameHeight = AppWindow.Size.Height - (int)Math.Round(Root.ActualHeight * scale);
        var contentHeight = Integration.PreferredContentHeight + Root.Padding.Top + Root.Padding.Bottom + 8;
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var height = Math.Min(area.Height, (int)Math.Ceiling(contentHeight * scale) + frameHeight);
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32(AppWindow.Size.Width, height));
    }
    private async void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_closeApproved)
        {
            return;
        }

        Integration.CommitPendingEdits();
        if (!Integration.IsBusy && !Integration.HasPendingChanges && !_confirmingClose)
        {
            return;
        }

        args.Cancel = true;
        if (await ConfirmDiscardAsync())
        {
            CloseAfterConfirmation();
        }
    }

    internal async Task<bool> ConfirmDiscardAsync()
    {
        if (_confirmingClose || Integration.IsBusy)
        {
            return false;
        }

        Integration.CommitPendingEdits();
        if (!Integration.HasPendingChanges)
        {
            return true;
        }

        _confirmingClose = true;
        try
        {
            Activate();
            var dialog = new ContentDialog
            {
                XamlRoot = Root.XamlRoot,
                RequestedTheme = Root.ActualTheme,
                Title = "Unapplied changes",
                Content = "There are pending changes that have not been applied. Go back to keep editing, or exit and discard these changes?",
                PrimaryButtonText = "Exit without applying",
                CloseButtonText = "Go back",
                DefaultButton = ContentDialogButton.Close
            };
            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }
        finally
        {
            _confirmingClose = false;
        }
    }

    internal void CloseAfterConfirmation()
    {
        _closeApproved = true;
        Close();
    }

    internal void ApplyAppearance(AppPreferences preferences) => ThemeService.Apply(Root, preferences);
}
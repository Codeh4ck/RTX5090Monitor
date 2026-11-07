using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using Rtx5090Monitoring.Client.Http;
using Rtx5090Monitoring.Contracts.Configuration;

namespace Rtx5090Monitoring.App.Services;

internal sealed partial class AppRuntime
{
    private WarningShutdownReason? _lastWarningShutdown;
    private bool _shutdownDialogOpen;
    private bool _readingShutdownReason;

    private async Task ReadLastWarningShutdownAsync()
    {
        if (_readingShutdownReason || _diagnostic || IsDemo)
        {
            return;
        }

        _readingShutdownReason = true;
        try
        {
            var api = _api;
            var reason = await api.GetWarningShutdownAsync(StopToken);
            if (reason is null || !ReferenceEquals(api, _api))
            {
                return;
            }

            _lastWarningShutdown = reason;
            await ShowLastWarningShutdownAsync();
        }
        catch (Exception error) when (error is HttpRequestException or MonitoringApiException
            or OperationCanceledException or System.Text.Json.JsonException)
        {
            System.Diagnostics.Debug.WriteLine(error);
        }
        finally
        {
            _readingShutdownReason = false;
        }
    }

    private async Task ShowLastWarningShutdownAsync()
    {
        if (_shutdownDialogOpen || !_rendering || _lastWarningShutdown is not { } reason
            || _window is not { } window || !window.AppWindow.IsVisible || window.ThemeRoot.XamlRoot is not { } root
            || window.AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized })
        {
            return;
        }

        _shutdownDialogOpen = true;
        var api = _api;
        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = root,
                Title = "RTX Monitor requested a shutdown",
                Content = new TextBlock
                {
                    Text = $"{reason.Warning}\nProfile: {reason.ProfileName}\n{reason.Detail}\n\n{reason.RequestedAt.ToLocalTime():g}",
                    TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
                },
                PrimaryButtonText = "OK",
                DefaultButton = ContentDialogButton.Primary
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await api.AcknowledgeWarningShutdownAsync(reason.Id, StopToken);
                _lastWarningShutdown = null;
            }
        }
        catch (Exception error) when (error is HttpRequestException or MonitoringApiException
            or OperationCanceledException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            System.Diagnostics.Debug.WriteLine(error);
        }
        finally
        {
            _shutdownDialogOpen = false;
        }
    }
}

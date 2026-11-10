using RTXLens.App.Updates;
using RTXLens.Client.Updates;
using RTXLens.Contracts.Updates;
using RTXLens.Presentation.Updates;

namespace RTXLens.App.Services;

internal sealed partial class AppRuntime
{
    private ReleaseNotesWindow? _releaseNotesWindow;
    private Task<ReleaseNotes?>? _releaseNotesRequest;
    private ReleaseNotes? _cachedReleaseNotes;
    private string? _prefetchedReleaseTag;
    private int _releaseNotesRequestId;

    private void PrefetchAvailableReleaseNotes()
    {
        var tag = Updates.Available.FirstOrDefault()?.ReleaseTag;
        if (tag is null || _prefetchedReleaseTag == tag)
        {
            return;
        }

        _prefetchedReleaseTag = tag;
        _ = FetchReleaseNotesAsync();
    }

    internal async Task OpenReleaseNotesAsync(UpdateNotice? update = null)
    {
        if (_window is null)
        {
            return;
        }

        var window = _releaseNotesWindow;
        if (window is null)
        {
            window = new ReleaseNotesWindow(Preferences);
            TrackWindow(window, "release-notes", 700, 490);
            _releaseNotesWindow = window;
            window.Closed += (_, _) => _releaseNotesWindow = null;
        }

        var requestId = ++_releaseNotesRequestId;
        window.Loading(update);
        window.Activate();

        var notes = update is not null && _cachedReleaseNotes?.ReleaseTag == update.ReleaseTag
            ? _cachedReleaseNotes : await FetchReleaseNotesAsync();
        if (_releaseNotesWindow != window || requestId != _releaseNotesRequestId || StopToken.IsCancellationRequested)
        {
            return;
        }

        if (notes is null || update is not null && notes.ReleaseTag != update.ReleaseTag)
        {
            window.Unavailable(update, differentRelease: notes is not null);
            return;
        }

        window.Display(notes, update);
    }

    private Task<ReleaseNotes?> FetchReleaseNotesAsync() => _releaseNotesRequest is { IsCompleted: false }
        ? _releaseNotesRequest : _releaseNotesRequest = ReadReleaseNotesAsync();

    private async Task<ReleaseNotes?> ReadReleaseNotesAsync()
    {
        try
        {
            return _cachedReleaseNotes = await new ReleaseNotesClient(_updateHttp).GetAsync(StopToken);
        }
        catch (Exception error) when (error is HttpRequestException or IOException or System.Text.Json.JsonException or OperationCanceledException)
        {
            return _cachedReleaseNotes;
        }
    }
}

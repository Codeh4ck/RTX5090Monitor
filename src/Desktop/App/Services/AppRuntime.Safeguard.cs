using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Microsoft.UI.Windowing;
using RTXLens.App.Msi.Afterburner;
using RTXLens.Contracts.Sensors;
using RTXLens.Plugins.Msi.Contracts.Safeguard;

namespace RTXLens.App.Services;

internal sealed partial class AppRuntime
{
    private SafeguardStorage? _safeguardStorage;
    private Guid _safeguardConsumerId;
    private Dictionary<Guid, DateTimeOffset> _seenSafeguardExecutions = [];
    private SafeguardExecutionNotice[] _safeguardNotices = [];

    internal SafeguardHistoryWindow? SafeguardHistory { get; private set; }
    internal bool HasSafeguardHistory { get; private set; }
    internal event Action? SafeguardHistoryChanged;

    private void InitializeSafeguard()
    {
        if (IsDemo)
        {
            return;
        }

        using var identity = WindowsIdentity.GetCurrent();
        if (identity.User is { } user)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(user.Value));
            _safeguardConsumerId = new Guid(hash.AsSpan(0, 16));
        }

        try
        {
            if (Directory.Exists(SafeguardStorage.DefaultRoot))
            {
                var storage = OpenSafeguardStorage();
                var cutoff = DateTimeOffset.UtcNow.AddDays(-storage.RetentionDays);
                HasSafeguardHistory = storage.Ids("history").Any(id =>
                    storage.Read<SafeguardExecution>("history", id) is { } execution && execution.StartedAt >= cutoff);
            }
        }
        catch (Exception error) when (SafeguardScriptDialog.IsConfigurationError(error))
        {
            System.Diagnostics.Debug.WriteLine(error);
        }
    }

    private SafeguardStorage OpenSafeguardStorage()
    {
        if (_safeguardStorage is { } existing)
        {
            return existing;
        }

        var storage = new SafeguardStorage();
        if (_safeguardConsumerId != Guid.Empty)
        {
            var saved = storage.Read<Dictionary<Guid, DateTimeOffset>>("seen", _safeguardConsumerId) ?? [];
            foreach (var entry in saved)
            {
                _seenSafeguardExecutions.TryAdd(entry.Key, entry.Value);
            }
        }

        _safeguardStorage = storage;
        return storage;
    }

    private void ReceiveSafeguard(SafeguardExecutionNotice[] notices)
    {
        if (!notices.SequenceEqual(_safeguardNotices) || HasSafeguardHistory != (notices.Length > 0))
        {
            _safeguardNotices = notices;
            HasSafeguardHistory = notices.Length > 0;
            SafeguardHistoryChanged?.Invoke();
        }

        if (IsDemo || notices.Length == 0)
        {
            return;
        }

        try
        {
            OpenSafeguardStorage();
        }
        catch (Exception error) when (SafeguardScriptDialog.IsConfigurationError(error))
        {
            System.Diagnostics.Debug.WriteLine(error);
        }

        var pending = notices.Where(notice => _seenSafeguardExecutions.TryAdd(notice.Id, notice.StartedAt)).ToArray();
        if (pending.Length == 0)
        {
            return;
        }

        if (pending.Length == 1)
        {
            var notice = pending[0];
            ShowNotification("Safeguard+ script executed", $"{notice.Script} · Connector {notice.Connector} · {notice.Status}");
        }
        else
        {
            ShowNotification("Safeguard+ scripts executed", $"{pending.Length} new events. Open Safeguard+ History for details.");
        }

        var oldest = DateTimeOffset.UtcNow.AddDays(-365);
        foreach (var id in _seenSafeguardExecutions.Where(pair => pair.Value < oldest).Select(pair => pair.Key).ToArray())
        {
            _seenSafeguardExecutions.Remove(id);
        }

        try
        {
            if (_safeguardConsumerId != Guid.Empty)
            {
                _safeguardStorage?.Write("seen", _safeguardConsumerId, _seenSafeguardExecutions);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine(error);
        }
    }

    internal void OpenSafeguardHistory()
    {
        if (SafeguardHistory is null)
        {
            var window = new SafeguardHistoryWindow(this);
            SafeguardHistory = window;
            window.Closed += (_, _) => SafeguardHistory = null;
            TrackWindow(window, "safeguard-history", 1040, 620);
        }

        SafeguardHistory.Activate();
    }

    internal void SetSafeguardHistoryAvailability(bool available)
    {
        if (HasSafeguardHistory != available)
        {
            HasSafeguardHistory = available;
            SafeguardHistoryChanged?.Invoke();
        }
    }
}
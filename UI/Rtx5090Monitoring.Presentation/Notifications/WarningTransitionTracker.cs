using Rtx5090Monitoring.Contracts.Sensors;
using Rtx5090Monitoring.Contracts.Warnings;
using Rtx5090Monitoring.Presentation.Settings;

namespace Rtx5090Monitoring.Presentation.Notifications;

public sealed class WarningTransitionTracker
{
    private readonly Dictionary<string, long> _seen = new(StringComparer.Ordinal);
    private string? _instance;
    private bool _enabled = true;

    public WarningNotification[] Evaluate(SensorSnapshot snapshot, DateTimeOffset now, AppPreferences? preferences = null)
    {
        var feed = snapshot.Warnings;
        var enabled = preferences?.WarningNotifications ?? true;
        if (!enabled)
        {
            _enabled = false;
            return [];
        }
        if (feed is null || !feed.NotificationsEnabled)
        {
            return [];
        }
        if (!_enabled || _instance != feed.InstanceId)
        {
            _seen.Clear();
        }
        _enabled = true;
        _instance = feed.InstanceId;
        var keys = feed.States.Select(state => state.Id + "/" + state.EpisodeId).ToHashSet(StringComparer.Ordinal);
        foreach (var old in _seen.Keys.Where(key => !keys.Contains(key)).ToArray())
        {
            _seen.Remove(old);
        }
        var due = new List<WarningState>();
        foreach (var state in feed.States.Where(state => state is { Phase: WarningPhase.Active, NotificationSequence: > 0 }
                                                         && (state.Policy.NotifyOnStart || state.Policy.RepeatWhileActive)))
        {
            var key = state.Id + "/" + state.EpisodeId;
            if (state.NotificationSequence > _seen.GetValueOrDefault(key))
            {
                _seen[key] = state.NotificationSequence;
                due.Add(state);
            }
        }
        if (due.Count == 0)
        {
            return [];
        }
        var title = due.Count == 1 ? due[0].Title : $"RTX Monitor - {due.Count} warnings";
        var message = string.Join(Environment.NewLine, due.Take(3).Select(state => due.Count == 1 ? state.Detail : state.Title + ": " + state.Detail));
        if (due.Count > 3)
        {
            message += $"{Environment.NewLine}+{due.Count - 3} more warnings";
        }
        return [new WarningNotification(feed.InstanceId + "/" + feed.Sequence, title, message)];
    }
}

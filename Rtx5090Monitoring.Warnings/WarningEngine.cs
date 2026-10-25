using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Contracts.Sensors;
using Rtx5090Monitoring.Contracts.Warnings;
using Rtx5090Monitoring.Warnings.State;

namespace Rtx5090Monitoring.Warnings;

public sealed class WarningEngine
{
    private readonly WarningRegistry _registry;
    private readonly Dictionary<string, WarningDefinition> _definitions;
    private readonly Dictionary<string, WarningEntry> _entries = new(StringComparer.Ordinal);
    private readonly string _instanceId = Guid.NewGuid().ToString("N");
    private WarningState[] _previous = [];
    private long _notificationSequence;
    private bool _enabled = true;

    public WarningEngine(WarningRegistry registry)
    {
        _registry = registry;
        _definitions = registry.Definitions.ToDictionary(definition => definition.Id, StringComparer.Ordinal);
    }

    public long Revision { get; private set; }

    public WarningSnapshot Evaluate(SensorSnapshot snapshot, ServiceConfiguration configuration, DateTimeOffset now)
    {
        var options = configuration.Warnings;
        if (!options.IsValid())
        {
            throw new ArgumentException("Invalid warning configuration.");
        }

        var observations = new Dictionary<string, WarningObservation>(StringComparer.Ordinal);
        foreach (var source in _registry.Sources)
        {
            if (!configuration.Plugins.TryGetValue(source.PluginId, out var settings) || !settings.Enabled)
            {
                continue;
            }

            foreach (var observation in source.Observe(snapshot, settings))
            {
                if (observation.PluginId != source.PluginId || !_definitions.TryGetValue(observation.TypeId, out var definition)
                    || definition.PluginId != source.PluginId || string.IsNullOrWhiteSpace(observation.DeviceId)
                    || !observations.TryAdd(observation.Id, observation))
                {
                    throw new InvalidOperationException("A plugin emitted an invalid or unregistered warning observation.");
                }
            }
        }

        var due = new List<WarningEntry>();
        foreach (var observation in observations.Values.Where(observation => observation is { Available: true, Active: true }))
        {
            if (!_entries.ContainsKey(observation.Id))
            {
                _entries.Add(observation.Id, new WarningEntry(observation, _definitions[observation.TypeId], options.PolicyFor(observation.TypeId), now));
            }
        }

        foreach (var (id, entry) in _entries.ToArray())
        {
            if (!configuration.Plugins.TryGetValue(entry.Observation.PluginId, out var plugin) || !plugin.Enabled
                || (!observations.Values.Any(observation => observation.PluginId == entry.Observation.PluginId && observation.DeviceId == entry.Observation.DeviceId)
                    && observations.Values.Any(observation => observation.PluginId == entry.Observation.PluginId)))
            {
                _entries.Remove(id);
                continue;
            }

            var policy = options.PolicyFor(entry.Observation.TypeId);
            var notifyEnabled = (!_enabled && options.NotificationsEnabled) || (!entry.Policy.NotifyOnStart && policy.NotifyOnStart);

            entry.Policy = policy;

            if (!observations.TryGetValue(id, out var current) || !current.Available)
            {
                entry.Phase = WarningPhase.Unavailable;
                entry.ClearSince = null;
                continue;
            }

            if (!current.Active)
            {
                entry.ClearSince ??= now;
                entry.Phase = WarningPhase.Recovering;

                if (now - entry.ClearSince >= TimeSpan.FromSeconds(policy.ClearAfterSeconds))
                {
                    _entries.Remove(id);
                }

                continue;
            }

            entry.Observation = current;
            entry.Phase = WarningPhase.Active;
            entry.ClearSince = null;

            if (options.NotificationsEnabled && (((entry.NotificationSequence == 0 || notifyEnabled) && policy.NotifyOnStart)
                || (policy.RepeatWhileActive && now - (entry.LastNotificationAt ?? entry.StartedAt) >= TimeSpan.FromSeconds(policy.RepeatIntervalSeconds))))
            {
                due.Add(entry);
            }
        }

        if (due.Count > 0)
        {
            _notificationSequence++;
            foreach (var entry in due)
            {
                entry.NotificationSequence = _notificationSequence;
                entry.LastNotificationAt = now;
            }
        }

        var states = _entries.Values.OrderBy(entry => entry.Observation.Id, StringComparer.Ordinal).Select(entry => entry.Snapshot()).ToArray();
        if (_enabled != options.NotificationsEnabled || !_previous.SequenceEqual(states))
        {
            Revision++;
        }

        _previous = states;
        _enabled = options.NotificationsEnabled;

        return new WarningSnapshot(_instanceId, _notificationSequence, _enabled, states);
    }
}

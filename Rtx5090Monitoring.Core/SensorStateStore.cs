using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Contracts.Health;
using Rtx5090Monitoring.Contracts.Alerts;
using Rtx5090Monitoring.Contracts.Sensors;
using Rtx5090Monitoring.Contracts.Monitoring;
using Rtx5090Monitoring.Core.Plugins;

namespace Rtx5090Monitoring.Core;

public sealed class SensorStateStore
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, PluginStoreEntry> _entries;

    private long _sequence;

    public SensorStateStore(IEnumerable<ISensorPlugin> plugins)
    {
        _entries = plugins.ToDictionary(p => p.Descriptor.Id,
            p => new PluginStoreEntry(p.Descriptor, new PluginSample(PluginState.Starting, []), null), StringComparer.Ordinal);

        foreach (var entry in _entries.Values)
        {
            if (string.IsNullOrWhiteSpace(entry.Descriptor.Id) || entry.Descriptor.PollInterval <= TimeSpan.Zero ||
                entry.Descriptor.StaleAfter < entry.Descriptor.PollInterval)
            {
                throw new ArgumentException("Plugin IDs and timing must be valid and unique.");
            }
        }
    }

    public void Register(PluginDescriptor descriptor)
    {
        lock (_lock)
        {
            if (_entries.TryGetValue(descriptor.Id, out var previous))
            {
                _entries[descriptor.Id] = previous with { Descriptor = descriptor };
            }
            else
            {
                _entries.Add(descriptor.Id, new PluginStoreEntry(descriptor, new PluginSample(PluginState.Disabled, []), null));
            }
        }
    }

    public void SetLifecycle(string pluginId, PluginState state)
    {
        lock (_lock)
        {
            var previous = _entries[pluginId];
            _entries[pluginId] = previous with { Sample = new PluginSample(state, []), LastSuccessAt = null };
        }
    }

    public void Update(string pluginId, PluginSample sample, DateTimeOffset now)
    {
        if (sample.State is not (PluginState.Ready or PluginState.NoDevice))
        {
            throw new ArgumentException("A poll result must be Ready or NoDevice.");
        }

        if (sample.State == PluginState.NoDevice && (sample.Sensors.Length != 0 || sample.Alerts is { Length: > 0 }))
        {
            throw new ArgumentException("A missing device cannot publish readings.");
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var reading in sample.Sensors)
        {
            if (!SensorValidation.IsValidIdentity(reading.Identity) || reading.Identity.PluginId != pluginId
                                                                    || reading.Unit != SensorUnits.For(reading.Identity.Metric) ||
                                                                    !keys.Add(reading.Identity.Key)
                                                                    || !Enum.IsDefined(reading.Quality) || reading.Quality == ReadingQuality.Stale
                                                                    || (reading.Value is { } value && !double.IsFinite(value))
                                                                    || reading is { Quality: ReadingQuality.Valid, Value: null }
                                                                    || reading.ObservedAt > now + TimeSpan.FromSeconds(1))
            {
                throw new ArgumentException("Invalid, duplicated, incorrectly owned, or non-finite sensor reading");
            }
        }

        foreach (var alert in sample.Alerts ?? [])
        {
            if (alert.PluginId != pluginId || string.IsNullOrWhiteSpace(alert.Kind) || alert.Pins is not { Length: > 0 }
                || alert.Pins.Any(pin => pin.Channel < 1 || !double.IsFinite(pin.CurrentA))
                || alert.Pins.Select(pin => pin.Channel).Distinct().Count() != alert.Pins.Length
                || alert.ReportedAt > now + TimeSpan.FromSeconds(1)
                || alert.PercentageDifference is { } percentage && !double.IsFinite(percentage)
                || sample.Sensors.All(reading => reading.Identity.DeviceId != alert.DeviceId))
            {
                throw new ArgumentException("Invalid plugin alert.");
            }
        }

        lock (_lock)
        {
            var previous = _entries[pluginId];

            // Copy plugin-owned arrays before publishing them to other threads.
            _entries[pluginId] = previous with
            {
                Sample = sample with
                {
                    Sensors = [.. sample.Sensors],
                    Alerts = sample.Alerts?.Select(alert => alert with { Pins = [.. alert.Pins] }).ToArray()
                },
                LastSuccessAt = sample.State == PluginState.Ready ? now : previous.LastSuccessAt
            };
        }
    }

    public void ReportError(string pluginId, string message)
    {
        lock (_lock)
        {
            var previous = _entries[pluginId];
            _entries[pluginId] = previous with { Sample = previous.Sample with { State = PluginState.Error, Message = message } };
        }
    }

    public SensorSnapshot Snapshot(DateTimeOffset now, IReadOnlyList<SensorSelection> selections)
    {
        lock (_lock)
        {
            var readings = new List<SensorReading>();
            var health = new List<PluginHealth>();
            var alerts = new List<SensorAlert>();

            foreach (var entry in _entries.Values.OrderBy(x => x.Descriptor.Id, StringComparer.Ordinal))
            {
                var expired = entry.LastSuccessAt is { } success && now - success > entry.Descriptor.StaleAfter;
                var state = entry.Sample.State == PluginState.Ready && expired ? PluginState.Stale : entry.Sample.State;

                health.Add(new PluginHealth(entry.Descriptor.Id, state, entry.LastSuccessAt, entry.Sample.Message));
                if (state == PluginState.Ready)
                {
                    alerts.AddRange(entry.Sample.Alerts ?? []);
                }

                readings.AddRange(from reading in entry.Sample.Sensors
                    let quality = entry.Sample.State != PluginState.Ready ? ReadingQuality.Unavailable : now - reading.ObservedAt > entry.Descriptor.StaleAfter ? ReadingQuality.Stale : reading.Quality
                    select reading with { Quality = quality });
            }

            var byKey = readings.ToDictionary(x => x.Identity.Key, StringComparer.Ordinal);

            return new SensorSnapshot(1, ++_sequence, now, [.. readings], [.. health], [.. selections.Select(x => SourceResolver.Resolve(x, byKey))])
            {
                Alerts = [.. alerts]
            };
        }
    }
}

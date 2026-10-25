using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Contracts.Sensors;
using Rtx5090Monitoring.Contracts.Monitoring;

namespace Rtx5090Monitoring.Core;

public static class SourceResolver
{
    public static ResolvedSelection Resolve(SensorSelection selection, IReadOnlyDictionary<string, SensorReading> sensors)
    {
        var limit = selection.AllowFallback ? selection.PreferredSources.Count : Math.Min(1, selection.PreferredSources.Count);

        for (var i = 0; i < limit; i++)
        {
            var key = selection.PreferredSources[i].Key;

            if (sensors.TryGetValue(key, out var reading) && reading is { Quality: ReadingQuality.Valid, Value: not null })
            {
                return new ResolvedSelection(selection.Name, i == 0 ? SelectionState.Selected : SelectionState.Fallback, key);
            }
        }

        return new ResolvedSelection(selection.Name, SelectionState.Unavailable, null);
    }
}
using RTXLens.Contracts.Sensors;
using RTXLens.Plugins.Msi.Contracts.Safeguard;

namespace RTXLens.Service.Services;

internal sealed class SafeguardTelemetryBuffer
{
    private const int Capacity = 128;
    internal static readonly TimeSpan MaximumCaptureAge = TimeSpan.FromSeconds(30);
    private readonly Lock _gate = new();
    private readonly Queue<SafeguardTelemetry> _frames = new(Capacity);

    public void Observe(SensorSnapshot snapshot)
    {
        lock (_gate)
        {
            if (_frames.Count == Capacity)
            {
                _frames.Dequeue();
            }

            _frames.Enqueue(new(snapshot.Timestamp, snapshot.Sensors) { SettingsProfile = snapshot.SettingsProfile });
        }
    }

    public SafeguardTelemetry? CaptureAt(DateTimeOffset trigger)
    {
        lock (_gate)
        {
            var frame = _frames.LastOrDefault(candidate => candidate.CapturedAt <= trigger
                && trigger - candidate.CapturedAt <= MaximumCaptureAge);
            return frame is null ? null : frame with
            {
                Readings = [.. frame.Readings.Where(reading => reading.ObservedAt <= trigger)]
            };
        }
    }
}

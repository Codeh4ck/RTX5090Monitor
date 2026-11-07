using Rtx5090Monitoring.Contracts.Configuration;

namespace Rtx5090Monitoring.Service.Services;

internal sealed class HardwarePresenceTracker
{
    private readonly Dictionary<string, int> _absences = new(StringComparer.Ordinal);

    public PluginSettings? Observe(string id, PluginSettings settings, bool? present)
    {
        if (present is null || settings is { Enabled: false, AutomaticallyDisabled: false })
        {
            _absences.Remove(id);
            return null;
        }

        _absences[id] = present.Value ? 0 : _absences.GetValueOrDefault(id) + 1;
        return present.Value && settings.AutomaticallyDisabled || !present.Value && settings.Enabled && _absences[id] >= 2
            ? settings with { Enabled = present.Value, AutomaticallyDisabled = !present.Value } : null;
    }
}

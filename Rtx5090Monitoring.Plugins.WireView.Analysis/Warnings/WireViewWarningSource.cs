using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Contracts.Sensors;
using Rtx5090Monitoring.Contracts.Warnings;
using Rtx5090Monitoring.Plugins.WireView.Contracts.Warnings;
using Rtx5090Monitoring.Warnings;

namespace Rtx5090Monitoring.Plugins.WireView.Analysis.Warnings;

public sealed class WireViewWarningSource : IWarningSource
{
    public string PluginId => "wireview-pro2";
    public IReadOnlyList<WarningDefinition> Definitions => [
        new(WireViewWarningIds.Overtemperature, PluginId, "Overtemperature", "temperature") { LegacyIds = ["WireViewOvertemperature"] },
        new(WireViewWarningIds.Overcurrent, PluginId, "Total overcurrent", "current") { LegacyIds = ["WireViewOvercurrent"] },
        new(WireViewWarningIds.PinOvercurrent, PluginId, "Pin overcurrent", "current") { LegacyIds = ["WireViewPinOvercurrent"] },
        new(WireViewWarningIds.Overpower, PluginId, "Overpower", "power") { LegacyIds = ["WireViewOverpower"] },
        new(WireViewWarningIds.Imbalance, PluginId, "Current imbalance", "imbalance") { LegacyIds = ["WireViewImbalance"] },
        new(WireViewWarningIds.UnknownFault, PluginId, "Unrecognized device fault", "warning") { LegacyIds = ["WireViewUnknownFault"] },
    ];
    public WarningObservation[] Observe(SensorSnapshot snapshot, PluginSettings settings)
    {
        if (!settings.Enabled || !snapshot.Plugins.Any(health => health.PluginId == PluginId && health.State == PluginState.Ready))
        {
            return [];
        }

        var observations = new List<WarningObservation>();

        var statuses = snapshot.Sensors.Where(reading => reading.Identity.PluginId == PluginId && reading.Identity.Metric == SensorMetric.FaultStatus
            && reading is { Quality: ReadingQuality.Valid, Value: { } value } && double.IsFinite(value)
            && snapshot.Timestamp - reading.ObservedAt <= TimeSpan.FromMilliseconds(settings.StaleAfterMs));

        foreach (var reading in statuses)
        {
            var flags = (ushort)reading.Value!.Value;

            Fault(WireViewWarningIds.Overtemperature, 0x02);
            Fault(WireViewWarningIds.Overcurrent, 0x04);
            Fault(WireViewWarningIds.PinOvercurrent, 0x08);
            Fault(WireViewWarningIds.Overpower, 0x10);
            Fault(WireViewWarningIds.Imbalance, 0x20);
            Fault(WireViewWarningIds.UnknownFault, unchecked((ushort)~0x3E));

            continue;

            void Fault(string typeId, ushort mask) => observations.Add(new WarningObservation(PluginId, reading.Identity.DeviceId, typeId,
                true, (flags & mask) != 0, "Reported by WireView firmware."));
        }
        
        return [.. observations];
    }
}

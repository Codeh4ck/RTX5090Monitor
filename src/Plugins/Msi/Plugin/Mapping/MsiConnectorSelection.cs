using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Contracts.Sensors;

namespace Rtx5090Monitoring.Plugins.Msi.Mapping;

public sealed class MsiConnectorSelection(string pluginId)
{
    private string? _device;
    private string? _lastConnector;

    public SensorIdentity[] Select(SensorSnapshot snapshot, string? requestedConnector)
    {
        var sensors = snapshot.Sensors.Where(reading => reading.Identity.PluginId == pluginId).ToArray();
        var devices = sensors.Select(reading => reading.Identity.DeviceId).ToHashSet();

        if (devices.Count != 1 || !snapshot.Plugins.Any(plugin => plugin.PluginId == pluginId && plugin.State == PluginState.Ready))
        {
            return [];
        }

        var firstDevice = devices.First();
        if (_device != firstDevice)
        {
            _device = firstDevice;
            _lastConnector = null;
        }

        var connector = requestedConnector;
        if (connector is null)
        {
            var pins = sensors.Where(reading => reading is { Identity: { Component: SensorComponent.PsuPin, Metric: SensorMetric.Current }, Quality: ReadingQuality.Valid, Value: >= 0 }).ToArray();
            if (pins.Length != 12 || pins.Select(pin => (pin.Identity.ConnectorId, pin.Identity.Channel)).Distinct().Count() != 12)
            {
                return [];
            }

            var active = pins.Where(pin => pin.Value > 0).Select(pin => pin.Identity.ConnectorId).Distinct().ToArray();
            if (active.Length > 1)
            {
                return [];
            }

            connector = active.Length == 1 ? active[0] : _lastConnector;
            _lastConnector = connector;
        }

        return connector is null ? [] : [.. sensors.Where(reading => reading.Identity is { Component: SensorComponent.PsuConnector, Metric: SensorMetric.Current }
            && reading.Identity.ConnectorId == connector).Select(reading => reading.Identity)];
    }
}

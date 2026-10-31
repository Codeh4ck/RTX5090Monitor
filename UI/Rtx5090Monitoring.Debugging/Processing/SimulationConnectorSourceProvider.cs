using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Contracts.Sensors;
using Rtx5090Monitoring.Core.Plugins;
using Rtx5090Monitoring.Plugins.Msi.Mapping;

namespace Rtx5090Monitoring.Debugging.Processing;

internal sealed class SimulationConnectorSourceProvider(string id) : IConnectorSourceProvider
{
    private readonly MsiConnectorSelection _msi = new(id);
    public string Id => id;
    public bool IsDedicatedProfile => id == "wireview-pro2";
    public string DedicatedProfileName => "WireView";
    public int? Priority(SensorMetric metric) => (id, metric) switch
    {
        ("generic-nvidia", SensorMetric.Power or SensorMetric.Current) => 0,
        ("generic-nvidia", SensorMetric.Voltage) => 100,
        ("asus-astral", SensorMetric.Power or SensorMetric.Current) => 10,
        ("asus-astral", SensorMetric.Voltage) => 0,
        ("wireview-pro2", SensorMetric.Power or SensorMetric.Current or SensorMetric.Voltage) => 20,
        ("msi-psu", SensorMetric.Current) => 30,
        _ => null
    };

    public SensorIdentity[] ConnectorSources(SensorSnapshot snapshot, PluginSettings settings, string? connectorId) =>
        id == "msi-psu" ? _msi.Select(snapshot, connectorId)
        : [.. snapshot.Sensors.Where(reading => reading.Identity.PluginId == id
                                                && reading.Identity is { Component: SensorComponent.GpuConnector, Channel: 1 }).Select(reading => reading.Identity)];
}

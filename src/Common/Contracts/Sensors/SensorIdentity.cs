using System.Text.Json;
using Rtx5090Monitoring.Contracts.Enums;

namespace Rtx5090Monitoring.Contracts.Sensors;

public sealed record SensorIdentity(
    string PluginId,
    string DeviceId,
    SensorComponent Component,
    SensorMetric Metric,
    int? Channel = null,
    string? ConnectorId = null)
{
    public string Key => string.Join('/', Uri.EscapeDataString(PluginId), Uri.EscapeDataString(DeviceId),
        JsonNamingPolicy.CamelCase.ConvertName(Component.ToString()), Uri.EscapeDataString(ConnectorId ?? "default"),
        Channel?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "all",
        JsonNamingPolicy.CamelCase.ConvertName(Metric.ToString()));
}
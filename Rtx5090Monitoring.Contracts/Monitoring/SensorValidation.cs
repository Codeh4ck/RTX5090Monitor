using Rtx5090Monitoring.Contracts.Sensors;

namespace Rtx5090Monitoring.Contracts.Monitoring;

public static class SensorValidation
{
    public static bool IsValidIdentity(SensorIdentity id) =>
        !string.IsNullOrWhiteSpace(id.PluginId)
        && !string.IsNullOrWhiteSpace(id.DeviceId) && Enum.IsDefined(id.Component) &&
        Enum.IsDefined(id.Metric)
        && id.Channel is null or > 0
        && (id.ConnectorId is null || !string.IsNullOrWhiteSpace(id.ConnectorId));
}
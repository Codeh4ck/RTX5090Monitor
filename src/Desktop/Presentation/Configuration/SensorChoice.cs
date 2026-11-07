using Rtx5090Monitoring.Contracts.Sensors;

namespace Rtx5090Monitoring.Presentation.Configuration;

public sealed record SensorChoice(SensorIdentity Identity)
{
    public string Label => $"{Identity.PluginId} · {Identity.DeviceId}"
        + (Identity.ConnectorId is { } connector ? $" · connector {connector}" : string.Empty)
        + $" · {Identity.Component} {Identity.Channel} · {Identity.Metric}";
}

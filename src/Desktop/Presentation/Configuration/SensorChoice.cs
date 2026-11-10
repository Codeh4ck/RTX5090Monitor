using RTXLens.Contracts.Sensors;

namespace RTXLens.Presentation.Configuration;

public sealed record SensorChoice(SensorIdentity Identity)
{
    public string Label => $"{Identity.PluginId} · {Identity.DeviceId}"
        + (Identity.ConnectorId is { } connector ? $" · connector {connector}" : string.Empty)
        + $" · {Identity.Component} {Identity.Channel} · {Identity.Metric}";
}

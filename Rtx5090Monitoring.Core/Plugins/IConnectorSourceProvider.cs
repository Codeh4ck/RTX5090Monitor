using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Contracts.Sensors;

namespace Rtx5090Monitoring.Core.Plugins;

public interface IConnectorSourceProvider
{
    string Id { get; }
    int? Priority(SensorMetric metric);
    bool IsDedicatedProfile => false;
    string DedicatedProfileName => Id;
    SensorIdentity[] ConnectorSources(SensorSnapshot snapshot, PluginSettings settings, string? connectorId);
}

using RTXLens.Contracts.Configuration;
using RTXLens.Contracts.Enums;
using RTXLens.Contracts.Sensors;

namespace RTXLens.Core.Plugins;

public interface IConnectorSourceProvider
{
    string Id { get; }
    int? Priority(SensorMetric metric);
    bool IsDedicatedProfile => false;
    string DedicatedProfileName => Id;
    SensorIdentity[] ConnectorSources(SensorSnapshot snapshot, PluginSettings settings, string? connectorId);
}

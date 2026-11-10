using LibreHardwareMonitor.Hardware;
using RTXLens.Contracts.Enums;

namespace RTXLens.Plugins.Asus.Mapping;

public sealed record AsusSensorDefinition(string SourceName, SensorType SourceType, string FriendlyName,
    SensorComponent Component, SensorMetric Metric, int? Channel = null);

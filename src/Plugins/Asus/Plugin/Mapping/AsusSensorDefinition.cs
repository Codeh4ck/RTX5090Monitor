using LibreHardwareMonitor.Hardware;
using Rtx5090Monitoring.Contracts.Enums;

namespace Rtx5090Monitoring.Plugins.Asus.Mapping;

public sealed record AsusSensorDefinition(string SourceName, SensorType SourceType, string FriendlyName,
    SensorComponent Component, SensorMetric Metric, int? Channel = null);

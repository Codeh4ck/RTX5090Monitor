using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Contracts.Sensors;
using Rtx5090Monitoring.Contracts.Alerts;

namespace Rtx5090Monitoring.Core.Plugins;

public sealed record PluginSample(PluginState State, SensorReading[] Sensors, string? Message = null, SensorAlert[]? Alerts = null);

using RTXLens.Contracts.Enums;
using RTXLens.Contracts.Sensors;
using RTXLens.Contracts.Alerts;

namespace RTXLens.Core.Plugins;

public sealed record PluginSample(PluginState State, SensorReading[] Sensors, string? Message = null, SensorAlert[]? Alerts = null);

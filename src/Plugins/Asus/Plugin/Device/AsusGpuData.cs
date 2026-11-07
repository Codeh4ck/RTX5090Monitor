namespace Rtx5090Monitoring.Plugins.Asus.Device;

public sealed record AsusGpuData(string DeviceId, string HardwareName, AsusSensorValue[] Sensors, DateTimeOffset? ObservedAt = null);

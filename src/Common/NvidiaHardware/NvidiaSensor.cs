using LibreHardwareMonitor.Hardware;

namespace Rtx5090Monitoring.Hardware.Nvidia;

public sealed record NvidiaSensor(string Name, SensorType Type, int Index, float? Value);

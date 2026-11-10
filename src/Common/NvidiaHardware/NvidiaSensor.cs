using LibreHardwareMonitor.Hardware;

namespace RTXLens.Hardware.Nvidia;

public sealed record NvidiaSensor(string Name, SensorType Type, int Index, float? Value);

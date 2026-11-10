namespace RTXLens.Hardware.Nvidia;

public sealed record NvidiaSample(NvidiaDevice Device, DateTimeOffset ObservedAt, NvidiaSensor[] Sensors);

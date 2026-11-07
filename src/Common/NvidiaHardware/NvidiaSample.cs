namespace Rtx5090Monitoring.Hardware.Nvidia;

public sealed record NvidiaSample(NvidiaDevice Device, DateTimeOffset ObservedAt, NvidiaSensor[] Sensors);

namespace Rtx5090Monitoring.Hardware.Nvidia;

public interface INvidiaHardwareSource : IDisposable
{
    NvidiaDevice[] Discover();
    NvidiaSensor[] Read(NvidiaDevice device);
    void Reset();
}

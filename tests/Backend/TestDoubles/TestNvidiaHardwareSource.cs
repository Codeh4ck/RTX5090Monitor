using Rtx5090Monitoring.Hardware.Nvidia;

namespace Rtx5090Monitoring.Tests.TestDoubles;

internal sealed class TestNvidiaHardwareSource : INvidiaHardwareSource
{
    public NvidiaDevice[] Devices { get; set; } = [];
    public NvidiaSensor[] Sensors { get; set; } = [];
    public int Reads { get; private set; }
    public int Resets { get; private set; }

    public NvidiaDevice[] Discover() => Devices;

    public NvidiaSensor[] Read(NvidiaDevice device)
    {
        Reads++;
        return Sensors;
    }

    public void Reset() => Resets++;

    public void Dispose() { }
}

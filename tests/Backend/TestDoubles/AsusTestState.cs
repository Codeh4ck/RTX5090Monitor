using RTXLens.Plugins.Asus.Device;
using RTXLens.Tests.Fixtures;

namespace RTXLens.Tests.TestDoubles;

internal sealed class AsusTestState
{
    public AsusGpuData[] Data = [AsusTestData.Create(), AsusTestData.Create(AsusTestData.SecondDeviceId, 350)];
    public bool Connected = true;
    public bool FailReads;
    public int CreateCount;
    public int DisposeCount;
    public int PollCount;
    public int ConcurrentAccessDetected;
    public int ActiveOperations;
    public string? LastRequestedDeviceId;
}

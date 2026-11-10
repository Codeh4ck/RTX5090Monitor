using RTXLens.Plugins.Asus.Contracts.Common;
namespace RTXLens.Plugins.Asus.Device;

public interface IAsusGpuReader : IDisposable
{
    IReadOnlyList<AsusDeviceInfo> Devices { get; }
    void SetPollInterval(int milliseconds) { }

    AsusGpuData? Read(string? deviceId, CancellationToken cancellationToken);
}

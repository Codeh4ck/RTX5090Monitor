using Rtx5090Monitoring.Plugins.Asus.Contracts.Common;
namespace Rtx5090Monitoring.Plugins.Asus.Device;

public interface IAsusGpuReader : IDisposable
{
    IReadOnlyList<AsusDeviceInfo> Devices { get; }
    void SetPollInterval(int milliseconds) { }

    AsusGpuData? Read(string? deviceId, CancellationToken cancellationToken);
}

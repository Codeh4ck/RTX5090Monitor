using RTXLens.Plugins.Msi.Contracts.Configuration;
using RTXLens.Plugins.Msi.Contracts.Devices;
using RTXLens.Plugins.Msi.Contracts.Telemetry;

namespace RTXLens.Plugins.Msi.Device;

public interface IMsiPsuReader : IDisposable
{
    MsiDeviceInfo[] Devices { get; }
    MsiSnapshot? Read(MsiOptions options, DateTimeOffset observedAt, CancellationToken cancellationToken);
    void ReleaseFanControl();
}

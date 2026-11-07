using Rtx5090Monitoring.Plugins.Msi.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Devices;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Telemetry;

namespace Rtx5090Monitoring.Plugins.Msi.Device;

public interface IMsiPsuReader : IDisposable
{
    MsiDeviceInfo[] Devices { get; }
    MsiSnapshot? Read(MsiOptions options, DateTimeOffset observedAt, CancellationToken cancellationToken);
    void ReleaseFanControl();
}

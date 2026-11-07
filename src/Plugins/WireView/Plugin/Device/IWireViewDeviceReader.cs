using Rtx5090Monitoring.Plugins.WireView.Contracts.Common;

namespace Rtx5090Monitoring.Plugins.WireView.Device;

public interface IWireViewDeviceReader : IDisposable
{
    string PortName { get; }
    WireViewSensorData? TryReadSensors(CancellationToken cancellationToken);
    DeviceConfig? ReadConfig();
    byte[]? ReadConfigRaw();
    bool ClearFaults(ushort faultStatusMask, ushort faultLogMask);
    void Reset();
}

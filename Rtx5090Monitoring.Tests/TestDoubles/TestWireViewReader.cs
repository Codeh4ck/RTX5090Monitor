using Rtx5090Monitoring.Plugins.WireView.Contracts.Common;
using Rtx5090Monitoring.Plugins.WireView.Device;

namespace Rtx5090Monitoring.Tests.TestDoubles;

internal sealed class TestWireViewReader(WireViewTestState state) : IWireViewDeviceReader
{
    public string PortName => "COM3";

    public WireViewSensorData? TryReadSensors(CancellationToken cancellationToken) =>
        Access(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref state.PollCount);
            return state.Connected ? state.Sample : null;
        });

    public DeviceConfig? ReadConfig() => Access(() => state.ConfigReadFails ? null : new DeviceConfig
    {
        Version = 2,
        FriendlyName = "Test WireView",
        WireOcpFaultThresholdA = 9.2,
        TsFaultThresholdC = 80,
        FaultDisplayEnable = 0x3E,
        FaultHardPowerEnable = 0x08
    });

    public byte[]? ReadConfigRaw() => Access<byte[]?>(() => state.ConfigReadFails ? null : [0xEF, 0x05, 0x02, 0x41]);

    public bool ClearFaults(ushort faultStatusMask, ushort faultLogMask) =>
        Access(() =>
        {
            state.LastStatusMask = faultStatusMask;
            state.LastLogMask = faultLogMask;
            return !state.FaultWriteFails;
        });

    public void Reset()
    {
        // The fake reader does not own a serial handle.
    }

    public void Dispose() => Access(() => Interlocked.Increment(ref state.DisposeCount));

    private T Access<T>(Func<T> operation)
    {
        if (Interlocked.Increment(ref state.ConcurrentOperations) > 1)
        {
            Interlocked.Exchange(ref state.ConcurrentAccessDetected, 1);
        }

        try
        {
            Thread.Sleep(3);
            return operation();
        }
        finally
        {
            Interlocked.Decrement(ref state.ConcurrentOperations);
        }
    }
}

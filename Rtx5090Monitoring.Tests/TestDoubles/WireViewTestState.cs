using Rtx5090Monitoring.Plugins.WireView.Device;

namespace Rtx5090Monitoring.Tests.TestDoubles;

internal sealed class WireViewTestState
{
    public bool Connected { get; set; } = true;
    public bool ConfigReadFails { get; set; }
    public bool FaultWriteFails { get; set; }
    public bool FailCreation { get; set; }
    public int PollCount;
    public int CreateCount;
    public int DisposeCount;
    public int ConcurrentOperations;
    public int ConcurrentAccessDetected;
    public ushort LastStatusMask { get; set; }
    public ushort LastLogMask { get; set; }

    public WireViewSensorData Sample { get; set; } = new(
        [12, 12, 12, 12, 12, 12],
        [1, 2, 3, 4, 5, 6],
        [12, 24, 36, 48, 60, 72],
        30, 31, -1, 28, 600, 0x08, 0x28);
}

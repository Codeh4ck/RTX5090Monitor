using Rtx5090Monitoring.Plugins.Msi.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Devices;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Telemetry;
using Rtx5090Monitoring.Plugins.Msi.Device;

namespace Rtx5090Monitoring.Tests.TestDoubles;

internal sealed class TestMsiReader(MsiTestState state) : IMsiPsuReader
{
    public MsiDeviceInfo[] Devices => [new("test-psu", "MPG Ai1300TS")];
    public MsiSnapshot? Read(MsiOptions options, DateTimeOffset observedAt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        state.LastOptions = options;
        var controlled = options.FanControl is MsiFanControl.Manual or MsiFanControl.Curve;
        var duty = options.FanControl == MsiFanControl.Curve
            ? Rtx5090Monitoring.Plugins.Msi.Control.MsiFanCurveController.Interpolate(options.FanCurve.Points, 55)
            : options.FanControl == MsiFanControl.Manual ? options.FanDutyPercent : 0;

        duty = MsiFanCurve.EffectiveDutyPercent(duty);

        return !state.Available ? null : new MsiSnapshot("test-psu", "MPG Ai1300TS", "10", observedAt,
            [[1, 2, 3, 4, 5, 6], [0, 0, 0, 0, 0, 0]], 55, 1100, !controlled || duty == 0, duty)
        {
            TargetFanDutyPercent = controlled ? duty : null,
            FanControllerState = options.FanControl.ToString()
        };
    }
    public void ReleaseFanControl() => state.ReleaseCount++;
    public void Dispose() => ReleaseFanControl();
}

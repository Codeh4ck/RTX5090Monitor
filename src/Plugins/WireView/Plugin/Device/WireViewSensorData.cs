namespace Rtx5090Monitoring.Plugins.WireView.Device;

public sealed record WireViewSensorData(
    double[] PinVoltage, // V  (6 pins)
    double[] PinCurrent, // A  (6 pins)
    double[] PinPower, // W  (6 pins)
    double TempInC,
    double TempOutC,
    double ExtTemp1C,
    double ExtTemp2C,
    int PsuCapabilityW,
    ushort FaultStatus,
    ushort FaultLog)
{
    public double? ReportedAverageVoltageV { get; init; }
    public double? ReportedTotalCurrentA { get; init; }
    public double? ReportedTotalPowerW { get; init; }

    public double TotalCurrentA => ReportedTotalCurrentA ?? PinCurrent.Sum();
    public double TotalPowerW => ReportedTotalPowerW ?? PinPower.Sum();
}

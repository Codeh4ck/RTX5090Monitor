using Rtx5090Monitoring.Contracts.Enums;

namespace Rtx5090Monitoring.Contracts.Sensors;

public static class SensorUnits
{
    public static SensorUnit For(SensorMetric metric) => metric switch
    {
        SensorMetric.Power or SensorMetric.PowerCapability => SensorUnit.Watts,
        SensorMetric.Current => SensorUnit.Amperes,
        SensorMetric.Voltage => SensorUnit.Volts,
        SensorMetric.Temperature => SensorUnit.Celsius,
        SensorMetric.Clock => SensorUnit.Megahertz,
        SensorMetric.FanSpeed => SensorUnit.Rpm,
        SensorMetric.Utilization or SensorMetric.ImbalancePercentage or SensorMetric.FanDuty => SensorUnit.Percent,
        SensorMetric.MemoryUsed or SensorMetric.MemoryTotal or SensorMetric.MemoryFree => SensorUnit.Megabytes,
        SensorMetric.ImbalanceDetected or SensorMetric.OvercurrentDetected or SensorMetric.FanAutomatic => SensorUnit.Boolean,
        SensorMetric.FaultStatus or SensorMetric.FaultLog => SensorUnit.Bitmask,
        _ => throw new ArgumentOutOfRangeException(nameof(metric))
    };
}

using RTXLens.Plugins.WireView.Contracts.Common;
using RTXLens.Plugins.WireView.Contracts.Output;
using RTXLens.Plugins.WireView.Device;

namespace RTXLens.Plugins.WireView.Mapping;

internal static class WireViewSnapshotMapper
{
    public static SensorResponse Create(WireViewSensorData data, DateTimeOffset timestamp)
    {
        var pins = new PinReading[6];

        for (var index = 0; index < pins.Length; index++)
        {
            pins[index] = new PinReading
            {
                Voltage = data.PinVoltage[index],
                Current = data.PinCurrent[index],
                Power = data.PinPower[index]
            };
        }

        return new SensorResponse
        {
            Pins = pins,
            TempInC = data.TempInC,
            TempOutC = data.TempOutC,
            ExtTemp1C = data.ExtTemp1C,
            ExtTemp2C = data.ExtTemp2C,
            TotalCurrentA = data.TotalCurrentA,
            TotalPowerW = data.TotalPowerW,
            PsuCapabilityW = data.PsuCapabilityW,
            FaultStatus = data.FaultStatus,
            FaultLog = data.FaultLog,
            Timestamp = timestamp
        };
    }
}
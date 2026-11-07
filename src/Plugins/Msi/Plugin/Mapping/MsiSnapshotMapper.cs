using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Contracts.Sensors;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Telemetry;

namespace Rtx5090Monitoring.Plugins.Msi.Mapping;

internal static class MsiSnapshotMapper
{
    public static SensorReading[] Create(MsiSnapshot snapshot)
    {
        if (snapshot.PinCurrentsA.Length != 2 || snapshot.PinCurrentsA.Any(pins => pins.Length != 6 || pins.Any(value => !double.IsFinite(value) || value < 0))
            || !double.IsFinite(snapshot.TemperatureC) || !double.IsFinite(snapshot.FanRpm) || snapshot.FanRpm < 0
            || snapshot.FanDutyPercent is < 0 or > 100)
        {
            throw new IOException("Invalid MSI sensor sample.");
        }

        var readings = new List<SensorReading>();

        for (var connector = 0; connector < 2; connector++)
        {
            var id = (connector + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            for (var pin = 0; pin < 6; pin++)
            {
                Add(SensorComponent.PsuPin, SensorMetric.Current, snapshot.PinCurrentsA[connector][pin], pin + 1, id);
            }

            Add(SensorComponent.PsuConnector, SensorMetric.Current, snapshot.PinCurrentsA[connector].Sum(), connector: id);
        }

        Add(SensorComponent.Psu, SensorMetric.Temperature, snapshot.TemperatureC);
        Add(SensorComponent.Psu, SensorMetric.FanSpeed, snapshot.FanRpm, 1);
        Add(SensorComponent.Psu, SensorMetric.FanDuty, snapshot.FanDutyPercent, 1);
        Add(SensorComponent.Psu, SensorMetric.FanAutomatic, snapshot.AutomaticFan ? 1 : 0, 1);
        return [.. readings];

        void Add(SensorComponent component, SensorMetric metric, double value, int? channel = null, string? connector = null)
            => readings.Add(new SensorReading(new SensorIdentity(MsiPlugin.PluginId, snapshot.DeviceId, component, metric, channel, connector),
                SensorUnits.For(metric), value, snapshot.ObservedAt));
    }
}
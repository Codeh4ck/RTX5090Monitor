using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Contracts.Sensors;

namespace Rtx5090Monitoring.Presentation.Performance;

public sealed record PerformanceTrace(string Key, string Group, string Label, string Source, string DeviceId,
    string Unit, string Color, SensorIdentity Identity)
{
    public static PerformanceTrace? Create(SensorReading reading, string source, int colorIndex)
    {
        var identity = reading.Identity;
        var group = (identity.Component, identity.Metric) switch
        {
            (SensorComponent.GpuPin or SensorComponent.PsuPin, SensorMetric.Current) => "Pin current",
            (SensorComponent.GpuConnector or SensorComponent.PsuConnector, SensorMetric.Current) => "Total current",
            (SensorComponent.GpuConnector or SensorComponent.PsuConnector, SensorMetric.Power) => "Connector power",
            (SensorComponent.GpuConnector or SensorComponent.PsuConnector, SensorMetric.Voltage) => "Connector voltage",
            (SensorComponent.GpuCore, SensorMetric.Voltage) => "GPU core voltage",
            (SensorComponent.GpuCore or SensorComponent.GpuMemory or SensorComponent.GpuHotspot, SensorMetric.Temperature) => "GPU temperatures",
            _ => null
        };

        if (group is null)
        {
            return null;
        }

        var unit = group switch
        {
            "Pin current" or "Total current" => "A",
            "Connector power" => "W",
            "GPU core voltage" => "mV",
            "Connector voltage" => "V",
            _ => "°C"
        };
        if (reading.Unit != SensorUnits.For(identity.Metric))
        {
            return null;
        }

        var connector = identity.ConnectorId is { } id ? $" · Connector {id}"
            : identity.Component == SensorComponent.GpuConnector && identity.Channel is > 1 ? $" · Connector {identity.Channel}" : "";
        var metric = group == "Pin current" ? $"P{identity.Channel}"
            : identity.Metric == SensorMetric.Temperature ? identity.Component switch
            {
                SensorComponent.GpuMemory => "VRAM",
                SensorComponent.GpuHotspot => "Hotspot",
                _ => "Core"
            }
            : group;

        return new(identity.Key, group, $"{source}{connector} · {metric}", source, identity.DeviceId, unit, ColorFor(colorIndex), identity);
    }

    private static string ColorFor(int index)
    {
        var hue = (170 + index * 137.507764) % 360 / 60;
        const double chroma = 0.65;
        const double minimum = 0.25;
        var secondary = chroma * (1 - Math.Abs(hue % 2 - 1));
        var (red, green, blue) = (int)hue switch
        {
            0 => (chroma, secondary, 0d),
            1 => (secondary, chroma, 0d),
            2 => (0d, chroma, secondary),
            3 => (0d, secondary, chroma),
            4 => (secondary, 0d, chroma),
            _ => (chroma, 0d, secondary)
        };

        return $"#{(byte)Math.Round((red + minimum) * 255):X2}{(byte)Math.Round((green + minimum) * 255):X2}{(byte)Math.Round((blue + minimum) * 255):X2}";
    }
}

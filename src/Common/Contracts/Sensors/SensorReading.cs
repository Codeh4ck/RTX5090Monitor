using RTXLens.Contracts.Enums;

namespace RTXLens.Contracts.Sensors;

public sealed record SensorReading(SensorIdentity Identity, SensorUnit Unit, double? Value, DateTimeOffset ObservedAt, ReadingQuality Quality = ReadingQuality.Valid)
{
    public bool Derived { get; init; }
}
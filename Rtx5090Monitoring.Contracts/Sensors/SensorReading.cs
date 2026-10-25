using Rtx5090Monitoring.Contracts.Enums;

namespace Rtx5090Monitoring.Contracts.Sensors;

public sealed record SensorReading(SensorIdentity Identity, SensorUnit Unit, double? Value, DateTimeOffset ObservedAt, ReadingQuality Quality = ReadingQuality.Valid);
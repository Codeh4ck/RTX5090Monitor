namespace Rtx5090Monitoring.Plugins.Msi.Contracts.Telemetry;

public sealed record MsiSnapshot(string DeviceId, string Model, string Revision, DateTimeOffset ObservedAt,
    double[][] PinCurrentsA, double TemperatureC, double FanRpm, bool AutomaticFan, int FanDutyPercent);

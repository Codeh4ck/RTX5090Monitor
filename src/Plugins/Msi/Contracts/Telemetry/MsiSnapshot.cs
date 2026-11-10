namespace RTXLens.Plugins.Msi.Contracts.Telemetry;

public sealed record MsiSnapshot(string DeviceId, string Model, string Revision, DateTimeOffset ObservedAt,
    double[][] PinCurrentsA, double TemperatureC, double FanRpm, bool AutomaticFan, int FanDutyPercent)
{
    public int? TargetFanDutyPercent { get; init; }
    public string FanControllerState { get; init; } = "Unmanaged";
    public string? FanControllerMessage { get; init; }
}

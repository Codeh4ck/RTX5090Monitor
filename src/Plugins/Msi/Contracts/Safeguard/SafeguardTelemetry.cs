using RTXLens.Contracts.Configuration;
using RTXLens.Contracts.Sensors;

namespace RTXLens.Plugins.Msi.Contracts.Safeguard;

public sealed record SafeguardTelemetry(DateTimeOffset CapturedAt, SensorReading[] Readings)
{
    public ActiveSettingsProfile? SettingsProfile { get; init; }
}

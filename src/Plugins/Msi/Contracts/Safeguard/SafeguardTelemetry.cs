using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Contracts.Sensors;

namespace Rtx5090Monitoring.Plugins.Msi.Contracts.Safeguard;

public sealed record SafeguardTelemetry(DateTimeOffset CapturedAt, SensorReading[] Readings)
{
    public ActiveSettingsProfile? SettingsProfile { get; init; }
}

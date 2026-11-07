using System.Text.Json.Serialization;
using Rtx5090Monitoring.Contracts.Health;
using Rtx5090Monitoring.Contracts.Sensors;
using Rtx5090Monitoring.Contracts.Discovery;

namespace Rtx5090Monitoring.Service;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(SensorSnapshot))]
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(Rtx5090Monitoring.Contracts.Updates.ServiceVersionResponse))]
[JsonSerializable(typeof(PluginCatalog))]
[JsonSerializable(typeof(Rtx5090Monitoring.Contracts.Configuration.ConfigurationSnapshot))]
[JsonSerializable(typeof(Rtx5090Monitoring.Contracts.Configuration.ServiceConfiguration))]
[JsonSerializable(typeof(Rtx5090Monitoring.Contracts.Configuration.PluginSettings))]
[JsonSerializable(typeof(Rtx5090Monitoring.Contracts.Configuration.SettingsProfile))]
[JsonSerializable(typeof(Rtx5090Monitoring.Contracts.Configuration.SettingsProfileCatalog))]
[JsonSerializable(typeof(Rtx5090Monitoring.Contracts.Configuration.SettingsProfileRequest))]
[JsonSerializable(typeof(Rtx5090Monitoring.Contracts.Configuration.DeviceWarningProfile[]))]
[JsonSerializable(typeof(Rtx5090Monitoring.Contracts.Configuration.DeviceWarningProfileCatalog))]
[JsonSerializable(typeof(Rtx5090Monitoring.Contracts.Configuration.WarningShutdownReason))]
internal partial class AppJsonContext : JsonSerializerContext;

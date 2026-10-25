using System.Text.Json.Serialization;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Devices;
using Rtx5090Monitoring.Plugins.Asus.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.Asus.Contracts.Common;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Contracts.Discovery;
using Rtx5090Monitoring.Contracts.Errors;
using Rtx5090Monitoring.Contracts.Health;
using Rtx5090Monitoring.Contracts.Sensors;
using Rtx5090Monitoring.Plugins.WireView.Contracts.Common;
using Rtx5090Monitoring.Plugins.WireView.Contracts.Input;

namespace Rtx5090Monitoring.Client.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true, UseStringEnumConverter = true, WriteIndented = true)]
[JsonSerializable(typeof(SensorSnapshot))]
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(Rtx5090Monitoring.Contracts.Updates.ServiceVersionResponse))]
[JsonSerializable(typeof(Rtx5090Monitoring.Contracts.Updates.ReleaseManifest))]
[JsonSerializable(typeof(PluginCatalog))]
[JsonSerializable(typeof(ConfigurationSnapshot))]
[JsonSerializable(typeof(ServiceConfiguration))]
[JsonSerializable(typeof(PluginSettings))]
[JsonSerializable(typeof(AsusOptions))]
[JsonSerializable(typeof(MsiOptions))]
[JsonSerializable(typeof(MsiDeviceInfo[]))]
[JsonSerializable(typeof(AsusDeviceInfo[]))]
[JsonSerializable(typeof(DeviceConfig))]
[JsonSerializable(typeof(ClearFaultsRequest))]
[JsonSerializable(typeof(ErrorResponse))]
public partial class ClientJsonContext : JsonSerializerContext;

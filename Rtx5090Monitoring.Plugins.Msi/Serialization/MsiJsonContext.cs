using System.Text.Json.Serialization;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Devices;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Telemetry;

namespace Rtx5090Monitoring.Plugins.Msi.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(MsiOptions))]
[JsonSerializable(typeof(MsiSnapshot))]
[JsonSerializable(typeof(MsiDeviceInfo[]))]
public partial class MsiJsonContext : JsonSerializerContext;
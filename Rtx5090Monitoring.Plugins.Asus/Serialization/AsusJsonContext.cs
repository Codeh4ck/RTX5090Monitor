using Rtx5090Monitoring.Plugins.Asus.Contracts.Configuration;
using System.Text.Json.Serialization;
using Rtx5090Monitoring.Plugins.Asus.Contracts.Common;
using Rtx5090Monitoring.Plugins.Asus.Contracts.Input;
using Rtx5090Monitoring.Plugins.Asus.Contracts.Output;

namespace Rtx5090Monitoring.Plugins.Asus.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(AsusOptions))]
[JsonSerializable(typeof(GpuSensorSnapshot))]
[JsonSerializable(typeof(ConfigRequest))]
[JsonSerializable(typeof(ConfigResponse))]
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(AsusDeviceInfo[]))]
internal partial class AsusJsonContext : JsonSerializerContext;

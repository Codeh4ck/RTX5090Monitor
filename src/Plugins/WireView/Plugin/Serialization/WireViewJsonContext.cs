using System.Text.Json.Serialization;
using Rtx5090Monitoring.Plugins.WireView.Contracts.Common;
using Rtx5090Monitoring.Plugins.WireView.Contracts.Input;
using Rtx5090Monitoring.Plugins.WireView.Contracts.Output;

namespace Rtx5090Monitoring.Plugins.WireView.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DeviceConfig))]
[JsonSerializable(typeof(ConfigRequest))]
[JsonSerializable(typeof(ClearFaultsRequest))]
[JsonSerializable(typeof(ConfigResponse))]
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(RawConfigResponse))]
[JsonSerializable(typeof(SensorResponse))]
internal partial class WireViewJsonContext : JsonSerializerContext;

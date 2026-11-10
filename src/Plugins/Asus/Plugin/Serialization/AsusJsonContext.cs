using RTXLens.Plugins.Asus.Contracts.Configuration;
using System.Text.Json.Serialization;
using RTXLens.Plugins.Asus.Contracts.Common;
using RTXLens.Plugins.Asus.Contracts.Input;
using RTXLens.Plugins.Asus.Contracts.Output;

namespace RTXLens.Plugins.Asus.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(AsusOptions))]
[JsonSerializable(typeof(GpuSensorSnapshot))]
[JsonSerializable(typeof(ConfigRequest))]
[JsonSerializable(typeof(ConfigResponse))]
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(AsusDeviceInfo[]))]
internal partial class AsusJsonContext : JsonSerializerContext;

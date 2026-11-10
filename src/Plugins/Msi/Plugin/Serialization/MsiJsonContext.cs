using System.Text.Json.Serialization;
using RTXLens.Plugins.Msi.Contracts.Configuration;
using RTXLens.Plugins.Msi.Contracts.Devices;
using RTXLens.Plugins.Msi.Contracts.Telemetry;

namespace RTXLens.Plugins.Msi.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(MsiOptions))]
[JsonSerializable(typeof(MsiSnapshot))]
[JsonSerializable(typeof(MsiDeviceInfo[]))]
public partial class MsiJsonContext : JsonSerializerContext;
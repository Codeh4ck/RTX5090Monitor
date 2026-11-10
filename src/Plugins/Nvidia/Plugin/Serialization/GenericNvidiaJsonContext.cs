using System.Text.Json.Serialization;
using RTXLens.Contracts.Configuration;
using RTXLens.Plugins.GenericNvidia.Contracts.Configuration;
using RTXLens.Plugins.GenericNvidia.Contracts.Devices;

namespace RTXLens.Plugins.GenericNvidia.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(GenericNvidiaOptions))]
[JsonSerializable(typeof(NvidiaDeviceInfo[]))]
[JsonSerializable(typeof(PluginSettings))]
internal partial class GenericNvidiaJsonContext : JsonSerializerContext;

using System.Text.Json.Serialization;
using RTXLens.Plugins.GenericNvidia.Contracts.Configuration;

namespace RTXLens.Plugins.GenericNvidia.Analysis.Warnings;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(GenericNvidiaOptions))]
internal partial class GenericNvidiaWarningJsonContext : JsonSerializerContext;

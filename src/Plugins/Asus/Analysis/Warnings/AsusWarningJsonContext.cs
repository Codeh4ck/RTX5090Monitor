using System.Text.Json.Serialization;
using RTXLens.Plugins.Asus.Contracts.Configuration;

namespace RTXLens.Plugins.Asus.Analysis.Warnings;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(AsusOptions))]
internal partial class AsusWarningJsonContext : JsonSerializerContext;

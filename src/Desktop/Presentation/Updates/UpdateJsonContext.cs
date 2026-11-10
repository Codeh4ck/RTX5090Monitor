using System.Text.Json.Serialization;

namespace RTXLens.Presentation.Updates;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(UpdateCheckState))]
internal partial class UpdateJsonContext : JsonSerializerContext;

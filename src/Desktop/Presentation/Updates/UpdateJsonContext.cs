using System.Text.Json.Serialization;

namespace Rtx5090Monitoring.Presentation.Updates;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(UpdateCheckState))]
internal partial class UpdateJsonContext : JsonSerializerContext;

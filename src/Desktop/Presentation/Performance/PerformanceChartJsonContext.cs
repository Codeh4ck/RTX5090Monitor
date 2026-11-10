using System.Text.Json.Serialization;

namespace RTXLens.Presentation.Performance;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(PerformanceChartFrame))]
[JsonSerializable(typeof(PerformanceHover))]
[JsonSerializable(typeof(PerformanceRecordingDocument))]
public partial class PerformanceChartJsonContext : JsonSerializerContext;

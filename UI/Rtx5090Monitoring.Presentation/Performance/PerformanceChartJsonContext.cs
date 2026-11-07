using System.Text.Json.Serialization;

namespace Rtx5090Monitoring.Presentation.Performance;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(PerformanceChartFrame))]
[JsonSerializable(typeof(PerformanceRecordingDocument))]
public partial class PerformanceChartJsonContext : JsonSerializerContext;

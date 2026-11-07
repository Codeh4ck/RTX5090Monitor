using System.Text.Json.Serialization;

namespace Rtx5090Monitoring.Presentation.Settings;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true, WriteIndented = true)]
[JsonSerializable(typeof(AppPreferences))]
internal partial class PreferencesJsonContext : JsonSerializerContext;

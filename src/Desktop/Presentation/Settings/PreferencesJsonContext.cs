using System.Text.Json.Serialization;

namespace RTXLens.Presentation.Settings;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true, WriteIndented = true)]
[JsonSerializable(typeof(AppPreferences))]
internal partial class PreferencesJsonContext : JsonSerializerContext;

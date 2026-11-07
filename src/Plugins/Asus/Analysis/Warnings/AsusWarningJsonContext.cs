using System.Text.Json.Serialization;
using Rtx5090Monitoring.Plugins.Asus.Contracts.Configuration;

namespace Rtx5090Monitoring.Plugins.Asus.Analysis.Warnings;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(AsusOptions))]
internal partial class AsusWarningJsonContext : JsonSerializerContext;

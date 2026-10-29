using System.Text.Json.Serialization;
using Rtx5090Monitoring.Plugins.GenericNvidia.Contracts.Configuration;

namespace Rtx5090Monitoring.Plugins.GenericNvidia.Analysis.Warnings;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(GenericNvidiaOptions))]
internal partial class GenericNvidiaWarningJsonContext : JsonSerializerContext;

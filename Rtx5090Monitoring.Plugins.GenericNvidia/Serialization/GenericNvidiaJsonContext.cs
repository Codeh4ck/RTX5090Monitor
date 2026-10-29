using System.Text.Json.Serialization;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.GenericNvidia.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.GenericNvidia.Contracts.Devices;

namespace Rtx5090Monitoring.Plugins.GenericNvidia.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(GenericNvidiaOptions))]
[JsonSerializable(typeof(NvidiaDeviceInfo[]))]
[JsonSerializable(typeof(PluginSettings))]
internal partial class GenericNvidiaJsonContext : JsonSerializerContext;

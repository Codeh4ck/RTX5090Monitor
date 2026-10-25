using System.Text.Json.Serialization;
using Rtx5090Monitoring.Contracts.Errors;

namespace Rtx5090Monitoring.Core.Http;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ErrorResponse))]
internal partial class HttpJsonContext : JsonSerializerContext;

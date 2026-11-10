using System.Text.Json.Serialization;
using RTXLens.Contracts.Errors;

namespace RTXLens.Core.Http;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ErrorResponse))]
internal partial class HttpJsonContext : JsonSerializerContext;

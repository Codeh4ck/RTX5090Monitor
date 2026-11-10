using System.Text.Json.Serialization;

namespace RTXLens.Presentation.Msi.Afterburner;

[JsonSerializable(typeof(AfterburnerBackup))]
[JsonSerializable(typeof(AfterburnerOperation))]
[JsonSerializable(typeof(AfterburnerOperationResult))]
public sealed partial class AfterburnerJsonContext : JsonSerializerContext;
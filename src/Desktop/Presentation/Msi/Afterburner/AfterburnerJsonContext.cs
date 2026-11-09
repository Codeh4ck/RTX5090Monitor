using System.Text.Json.Serialization;

namespace Rtx5090Monitoring.Presentation.Msi.Afterburner;

[JsonSerializable(typeof(AfterburnerBackup))]
[JsonSerializable(typeof(AfterburnerOperation))]
[JsonSerializable(typeof(AfterburnerOperationResult))]
public sealed partial class AfterburnerJsonContext : JsonSerializerContext;
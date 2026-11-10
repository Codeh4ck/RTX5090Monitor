namespace RTXLens.Plugins.Msi.Contracts.Safeguard;

public sealed record SafeguardStep(DateTimeOffset Timestamp, string Name, string Result, int? ExitCode = null);

public sealed record SafeguardExecution(Guid Id, Guid BindingId, string ScriptId, int Version, int Connector,
    int DelayMs, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, string Status, SafeguardStep[] Steps)
{
    public string Name => SafeguardScripts.All.FirstOrDefault(script => script.Id == ScriptId)?.Name ?? ScriptId;
}

public sealed record SafeguardDelivery(string Status, int Attempts, DateTimeOffset NextAttempt, string? Detail = null);

public sealed record SafeguardMailEvent(Guid ExecutionId, Guid BindingId, DateTimeOffset StartedAt);
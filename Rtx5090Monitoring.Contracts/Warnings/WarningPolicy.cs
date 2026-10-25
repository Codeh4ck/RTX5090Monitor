namespace Rtx5090Monitoring.Contracts.Warnings;

public sealed record WarningPolicy
{
    public bool NotifyOnStart { get; init; } = true;
    public bool RepeatWhileActive { get; init; }
    public int RepeatIntervalSeconds { get; init; } = 5;
    public int ClearAfterSeconds { get; init; } = 2;

    public bool IsValid() => RepeatIntervalSeconds is >= 5 and <= 3600 && ClearAfterSeconds is >= 0 and <= 60;
}

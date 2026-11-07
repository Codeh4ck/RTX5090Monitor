namespace Rtx5090Monitoring.Contracts.Warnings;

public sealed record WarningSnapshot(string InstanceId, long Sequence, bool NotificationsEnabled, WarningState[] States);

namespace Rtx5090Monitoring.Presentation.Updates;

public sealed record UpdateDismissal(string Version, bool Skipped, DateTimeOffset? RemindAfter);

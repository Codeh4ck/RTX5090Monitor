namespace RTXLens.Presentation.Updates;

public sealed record UpdateDismissal(string Version, bool Skipped, DateTimeOffset? RemindAfter);

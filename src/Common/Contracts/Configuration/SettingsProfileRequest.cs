namespace Rtx5090Monitoring.Contracts.Configuration;

public sealed record SettingsProfileRequest(string ProfileId, SettingsProfile? Settings, bool Save);

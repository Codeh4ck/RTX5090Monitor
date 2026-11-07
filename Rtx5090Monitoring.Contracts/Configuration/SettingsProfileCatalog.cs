namespace Rtx5090Monitoring.Contracts.Configuration;

public sealed record SettingsProfileCatalog(string SelectedId, SettingsProfile? Effective, SettingsProfile[] Profiles, SettingsProfile? Automatic = null);

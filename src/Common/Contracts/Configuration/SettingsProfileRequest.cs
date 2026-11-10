namespace RTXLens.Contracts.Configuration;

public sealed record SettingsProfileRequest(string ProfileId, SettingsProfile? Settings, bool Save);

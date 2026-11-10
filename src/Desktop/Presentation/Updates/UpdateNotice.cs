namespace RTXLens.Presentation.Updates;

public sealed record UpdateNotice(string Component, string Name, string InstalledVersion, string AvailableVersion, string ReleaseUrl, string ReleaseTag)
{
    public string Description => $"{Name}: {InstalledVersion} → {AvailableVersion}";
}

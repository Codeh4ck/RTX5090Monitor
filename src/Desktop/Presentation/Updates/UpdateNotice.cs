namespace Rtx5090Monitoring.Presentation.Updates;

public sealed record UpdateNotice(string Component, string Name, string InstalledVersion, string AvailableVersion, string ReleaseUrl)
{
    public string Description => $"{Name}: {InstalledVersion} → {AvailableVersion}";
}

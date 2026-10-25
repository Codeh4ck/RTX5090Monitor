namespace Rtx5090Monitoring.Contracts.Updates;

public sealed record ReleaseManifest
{
    public int SchemaVersion { get; init; }
    public string ReleaseTag { get; init; } = string.Empty;
    public string Desktop { get; init; } = string.Empty;
    public string Service { get; init; } = string.Empty;
    public string Widget { get; init; } = string.Empty;

    public const string RepositoryUrl = "https://github.com/Codeh4ck/RTX5090Monitor";
    public const string ManifestUrl = RepositoryUrl + "/releases/latest/download/version.json";

    public string ReleaseUrl => RepositoryUrl + "/releases/tag/" + Uri.EscapeDataString(ReleaseTag);

    public bool IsValid() => SchemaVersion == 1
        && SemanticVersion.TryParse(Desktop, out _)
        && SemanticVersion.TryParse(Service, out _)
        && SemanticVersion.TryParse(Widget, out _)
        && ReleaseTag is { Length: > 1 } && ReleaseTag.StartsWith('v')
        && SemanticVersion.TryParse(ReleaseTag[1..], out _);
}

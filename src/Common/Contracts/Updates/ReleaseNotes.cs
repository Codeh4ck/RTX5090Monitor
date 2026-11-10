namespace RTXLens.Contracts.Updates;

public sealed record ReleaseNotes
{
    public const string FeedUrl = "https://rtxlens.com/updates/changelog.json";

    public int SchemaVersion { get; init; }
    public string ReleaseTag { get; init; } = string.Empty;
    public DateTimeOffset PublishedAt { get; init; }
    public string ReleaseUrl { get; init; } = string.Empty;
    public string Changelog { get; init; } = string.Empty;

    public bool IsValid() => SchemaVersion == 1
        && ReleaseTag is { Length: > 1 } && ReleaseTag.StartsWith('v')
        && SemanticVersion.TryParse(ReleaseTag[1..], out _)
        && PublishedAt != default
        && (ReleaseUrl == ReleaseManifest.RepositoryUrl + "/releases/tag/" + ReleaseTag
            || ReleaseUrl == "https://github.com/Codeh4ck/RTX5090Monitor/releases/tag/" + ReleaseTag)
        && !string.IsNullOrWhiteSpace(Changelog);

    public static Uri DownloadFor(string releaseUrl) =>
        new(releaseUrl.Replace("/releases/tag/", "/releases/download/", StringComparison.Ordinal) + "/RTXLens-win-x64.zip");
}

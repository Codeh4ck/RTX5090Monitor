using RTXLens.Contracts.Updates;

namespace RTXLens.Presentation.Updates;

public sealed class UpdateCheckState
{
    public DateTimeOffset? LastSuccessfulCheck { get; set; }
    public ReleaseManifest? Manifest { get; set; }
    public Dictionary<string, UpdateDismissal> Dismissals { get; set; } = [];
}

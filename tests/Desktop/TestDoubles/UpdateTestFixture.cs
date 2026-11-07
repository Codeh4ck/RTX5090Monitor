using Rtx5090Monitoring.Contracts.Updates;
using Rtx5090Monitoring.Presentation.Updates;

namespace Rtx5090Monitoring.UI.Tests.TestDoubles;

internal sealed class UpdateTestFixture : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), "rtx-update-tests-" + Guid.NewGuid().ToString("N"), "updates.json");

    public UpdateTestClock Clock { get; } = new();
    public ReleaseManifest Manifest { get; set; } = new()
    {
        SchemaVersion = 1,
        ReleaseTag = "v1.1.0",
        Desktop = "1.1.0",
        Service = "1.1.0",
        Widget = "1.0.0"
    };

    public string? ServiceVersion { get; set; } = "1.0.0";
    public int Requests { get; private set; }
    public bool Fail { get; set; }

    public UpdateViewModel Create() => new("1.0.0", FetchAsync, _ => Task.FromResult(ServiceVersion), new UpdateStateStore(_path), Clock);

    private Task<ReleaseManifest> FetchAsync(CancellationToken token)
    {
        Requests++;

        return Fail ? Task.FromException<ReleaseManifest>(new HttpRequestException("offline")) : Task.FromResult(Manifest);
    }

    public void Dispose()
    {
        var directory = Path.GetDirectoryName(_path)!;

        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}

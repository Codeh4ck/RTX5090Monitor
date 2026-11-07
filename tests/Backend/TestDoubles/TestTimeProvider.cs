namespace Rtx5090Monitoring.Tests.TestDoubles;

internal sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan duration) => _now += duration;
}

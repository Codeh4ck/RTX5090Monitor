namespace Rtx5090Monitoring.UI.Tests.TestDoubles;

internal sealed class UpdateTestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 1, 31, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;
}

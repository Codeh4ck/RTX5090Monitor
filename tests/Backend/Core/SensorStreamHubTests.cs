using Rtx5090Monitoring.Core.Streaming;
using Rtx5090Monitoring.Tests.Infrastructure;

namespace Rtx5090Monitoring.Tests.Core;

internal static class SensorStreamHubTests
{
    public static void Run(TestRunner runner)
    {
        runner.Run(KeepsTheNewestSnapshotForSlowClients);
        runner.Run(BoundsEachClientQueueToOneSnapshot);
        runner.Run(SendsTheCurrentSnapshotToNewSubscribers);
    }

    private static void KeepsTheNewestSnapshotForSlowClients()
    {
        using var hub = new SensorStreamHub();
        using var subscription = hub.Subscribe();

        hub.Publish([1]);
        hub.Publish([2]);
        hub.Publish([3]);

        var hasFrame = subscription.Reader.TryRead(out var frame);

        TestAssert.True(hasFrame, "A client must receive the latest snapshot.");
        TestAssert.Equal((byte)3, frame![0]);
    }

    private static void BoundsEachClientQueueToOneSnapshot()
    {
        using var hub = new SensorStreamHub();
        using var subscription = hub.Subscribe();

        hub.Publish([1]);
        hub.Publish([2]);
        subscription.Reader.TryRead(out _);

        var hasOlderFrame = subscription.Reader.TryRead(out _);

        TestAssert.False(hasOlderFrame, "Older frames must not accumulate behind the latest snapshot.");
    }

    private static void SendsTheCurrentSnapshotToNewSubscribers()
    {
        using var hub = new SensorStreamHub();
        hub.Publish([3]);

        using var subscription = hub.Subscribe();
        var hasFrame = subscription.Reader.TryRead(out var frame);

        TestAssert.True(hasFrame, "A new subscriber must receive the current cached snapshot.");
        TestAssert.Equal((byte)3, frame![0]);
    }
}

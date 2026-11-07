namespace Rtx5090Monitoring.Tests.TestDoubles;

internal enum TestPluginBehavior
{
    PublishReadings,
    ThrowOnPoll,
    WaitForCancellation
}

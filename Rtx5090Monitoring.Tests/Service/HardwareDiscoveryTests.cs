using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Service.Services;
using Rtx5090Monitoring.Tests.Infrastructure;

namespace Rtx5090Monitoring.Tests.Service;

internal static class HardwareDiscoveryTests
{
    public static void Run(TestRunner runner) => runner.Run(RequiresConfirmedAbsenceAndOnlyReenablesAutomaticallyDisabledPlugins);

    private static void RequiresConfirmedAbsenceAndOnlyReenablesAutomaticallyDisabledPlugins()
    {
        var tracker = new HardwarePresenceTracker();
        var enabled = new PluginSettings();
        TestAssert.True(tracker.Observe("hardware", enabled, false) is null, "One missing probe must not disable hardware.");
        tracker.Observe("hardware", enabled, null);
        TestAssert.True(tracker.Observe("hardware", enabled, false) is null, "An inconclusive probe must reset absence confirmation.");
        var disabled = tracker.Observe("hardware", enabled, false)!;
        TestAssert.False(disabled.Enabled, "Confirmed absence should disable polling.");
        TestAssert.True(disabled.AutomaticallyDisabled, "Automatic absence must be distinguished from manual disabling.");
        TestAssert.True(tracker.Observe("hardware", disabled, true) is { Enabled: true, AutomaticallyDisabled: false },
            "Returning hardware should be enabled again.");
        TestAssert.True(tracker.Observe("manual", new PluginSettings { Enabled = false }, true) is null,
            "A manual disable must never be reversed by discovery.");
    }
}

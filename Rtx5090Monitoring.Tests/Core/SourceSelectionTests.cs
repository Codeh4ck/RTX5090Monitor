using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Tests.Fixtures;
using Rtx5090Monitoring.Tests.Infrastructure;

namespace Rtx5090Monitoring.Tests.Core;

internal static class SourceSelectionTests
{
    public static void Run(TestRunner runner)
    {
        runner.Run(PreservesIdenticalMetricsFromDifferentPlugins);
        runner.Run(SelectsTheFirstPreferredSource);
        runner.Run(AllowsTheUserToPrioritizeAsus);
        runner.Run(FallsBackWhenThePreferredPluginFails);
        runner.Run(DoesNotSwitchSourcesWhenFallbackIsDisabled);
        runner.Run(DoesNotSelectStaleReadings);
    }

    private static void PreservesIdenticalMetricsFromDifferentPlugins()
    {
        var store = SensorTestData.CreatePopulatedStore();

        var snapshot = store.Snapshot(SensorTestData.Timestamp, []);
        var distinctKeys = snapshot.Sensors.Select(reading => reading.Identity.Key).Distinct();

        TestAssert.Equal(2, snapshot.Sensors.Length);
        TestAssert.Equal(2, distinctKeys.Count());
    }

    private static void SelectsTheFirstPreferredSource()
    {
        var store = SensorTestData.CreatePopulatedStore();
        var selection = SensorTestData.CreatePowerSelection();

        var snapshot = store.Snapshot(SensorTestData.Timestamp, [selection]);
        var resolvedSelection = snapshot.Selections.Single();

        TestAssert.Equal(SensorTestData.WireViewPower.Key, resolvedSelection.SensorKey);
        TestAssert.Equal(SelectionState.Selected, resolvedSelection.State);
    }

    private static void AllowsTheUserToPrioritizeAsus()
    {
        var store = SensorTestData.CreatePopulatedStore();
        var selection = SensorTestData.CreatePowerSelection(preferAsus: true);

        var snapshot = store.Snapshot(SensorTestData.Timestamp, [selection]);
        var resolvedSelection = snapshot.Selections.Single();

        TestAssert.Equal(SensorTestData.AsusPower.Key, resolvedSelection.SensorKey);
    }

    private static void FallsBackWhenThePreferredPluginFails()
    {
        var store = SensorTestData.CreatePopulatedStore();
        var selection = SensorTestData.CreatePowerSelection(preferAsus: true);

        store.ReportError(SensorTestData.AsusPower.PluginId, "Simulated polling failure.");
        var snapshot = store.Snapshot(SensorTestData.Timestamp, [selection]);
        var resolvedSelection = snapshot.Selections.Single();

        TestAssert.Equal(SelectionState.Fallback, resolvedSelection.State);
        TestAssert.Equal(SensorTestData.WireViewPower.Key, resolvedSelection.SensorKey);
    }

    private static void DoesNotSwitchSourcesWhenFallbackIsDisabled()
    {
        var store = SensorTestData.CreatePopulatedStore();
        var selection = SensorTestData.CreatePowerSelection(preferAsus: true, allowFallback: false);

        store.ReportError(SensorTestData.AsusPower.PluginId, "Simulated polling failure.");
        var snapshot = store.Snapshot(SensorTestData.Timestamp, [selection]);
        var resolvedSelection = snapshot.Selections.Single();

        TestAssert.Equal(SelectionState.Unavailable, resolvedSelection.State);
        TestAssert.Equal<string?>(null, resolvedSelection.SensorKey);
    }

    private static void DoesNotSelectStaleReadings()
    {
        var store = SensorTestData.CreatePopulatedStore();
        var selection = SensorTestData.CreatePowerSelection();
        var afterFreshnessLimit = SensorTestData.Timestamp.AddSeconds(6);

        var snapshot = store.Snapshot(afterFreshnessLimit, [selection]);

        TestAssert.Equal(SelectionState.Unavailable, snapshot.Selections.Single().State);
    }
}

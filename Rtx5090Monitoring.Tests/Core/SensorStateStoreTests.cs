using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Contracts.Sensors;
using Rtx5090Monitoring.Core.Plugins;
using Rtx5090Monitoring.Tests.Fixtures;
using Rtx5090Monitoring.Tests.Infrastructure;

namespace Rtx5090Monitoring.Tests.Core;

internal static class SensorStateStoreTests
{
    public static void Run(TestRunner runner)
    {
        runner.Run(MarksExpiredReadingsAsStale);
        runner.Run(MarksExpiredPluginsAsStale);
        runner.Run(RemovesReadingsWhenADeviceDisconnects);
        runner.Run(RejectsNaNValues);
        runner.Run(RejectsInfiniteValues);
        runner.Run(RejectsReadingsOwnedByAnotherPlugin);
        runner.Run(RejectsDuplicateSensorIdentities);
        runner.Run(RejectsUnitsThatDoNotMatchTheMetric);
        runner.Run(RejectsFutureTimestamps);
    }

    private static void MarksExpiredReadingsAsStale()
    {
        var store = SensorTestData.CreatePopulatedStore();
        var afterFreshnessLimit = SensorTestData.Timestamp.AddSeconds(6);

        var snapshot = store.Snapshot(afterFreshnessLimit, []);
        var wireViewReading = snapshot.Sensors.Single(reading => reading.Identity == SensorTestData.WireViewPower);

        TestAssert.Equal(ReadingQuality.Stale, wireViewReading.Quality);
    }

    private static void MarksExpiredPluginsAsStale()
    {
        var store = SensorTestData.CreatePopulatedStore();
        var afterFreshnessLimit = SensorTestData.Timestamp.AddSeconds(6);

        var snapshot = store.Snapshot(afterFreshnessLimit, []);
        var wireViewHealth = snapshot.Plugins.Single(plugin => plugin.PluginId == SensorTestData.WireViewPower.PluginId);

        TestAssert.Equal(PluginState.Stale, wireViewHealth.State);
    }

    private static void RemovesReadingsWhenADeviceDisconnects()
    {
        var store = SensorTestData.CreatePopulatedStore();
        var disconnectedSample = new PluginSample(PluginState.NoDevice, []);

        store.Update(SensorTestData.WireViewPower.PluginId, disconnectedSample, SensorTestData.Timestamp);
        var snapshot = store.Snapshot(SensorTestData.Timestamp, []);
        var hasWireViewReadings = snapshot.Sensors.Any(reading => reading.Identity.PluginId == SensorTestData.WireViewPower.PluginId);

        TestAssert.False(hasWireViewReadings, "Disconnected devices must not retain published readings.");
    }

    private static void RejectsNaNValues()
    {
        var reading = SensorTestData.CreatePowerReading(SensorTestData.WireViewPower, double.NaN);

        AssertReadingsAreRejected(reading);
    }

    private static void RejectsInfiniteValues()
    {
        var reading = SensorTestData.CreatePowerReading(SensorTestData.WireViewPower, double.PositiveInfinity);

        AssertReadingsAreRejected(reading);
    }

    private static void RejectsReadingsOwnedByAnotherPlugin()
    {
        var asusReading = SensorTestData.CreatePowerReading(SensorTestData.AsusPower);

        AssertReadingsAreRejected(asusReading);
    }

    private static void RejectsDuplicateSensorIdentities()
    {
        var firstReading = SensorTestData.CreatePowerReading(SensorTestData.WireViewPower, value: 400);
        var duplicateReading = SensorTestData.CreatePowerReading(SensorTestData.WireViewPower, value: 401);

        AssertReadingsAreRejected(firstReading, duplicateReading);
    }

    private static void RejectsUnitsThatDoNotMatchTheMetric()
    {
        var reading = SensorTestData.CreatePowerReading(SensorTestData.WireViewPower) with
        {
            Unit = SensorUnit.Amperes
        };

        AssertReadingsAreRejected(reading);
    }

    private static void RejectsFutureTimestamps()
    {
        var reading = SensorTestData.CreatePowerReading(SensorTestData.WireViewPower) with
        {
            ObservedAt = SensorTestData.Timestamp.AddMinutes(1)
        };

        AssertReadingsAreRejected(reading);
    }

    private static void AssertReadingsAreRejected(params SensorReading[] readings)
    {
        var store = SensorTestData.CreateStore();
        var sample = new PluginSample(PluginState.Ready, readings);

        TestAssert.Throws<ArgumentException>(() =>
        {
            store.Update(SensorTestData.WireViewPower.PluginId, sample, SensorTestData.Timestamp);
        });
    }
}

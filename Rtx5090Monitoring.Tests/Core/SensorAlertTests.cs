using Rtx5090Monitoring.Plugins.Asus.Contracts.Warnings;
using Rtx5090Monitoring.Contracts.Alerts;
using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Core.Plugins;
using Rtx5090Monitoring.Tests.Fixtures;
using Rtx5090Monitoring.Tests.Infrastructure;

namespace Rtx5090Monitoring.Tests.Core;

internal static class SensorAlertTests
{
    public static void Run(TestRunner runner)
    {
        runner.Run(DoesNotPublishExpiredOrFailedAlerts);
        runner.Run(CopiesPluginOwnedAlertArrays);
        runner.Run(RejectsInvalidAlertOwnershipAndPins);
    }

    private static SensorAlert CreateAlert() => new("asus-astral", "gpu0", AsusAlertKinds.WireOvercurrent, SensorTestData.Timestamp, [new AlertPin(1, 10)]);

    private static PluginSample CreateSample(SensorAlert alert) => new(PluginState.Ready,
        [SensorTestData.CreatePowerReading(SensorTestData.AsusPower)], Alerts: [alert]);

    private static void DoesNotPublishExpiredOrFailedAlerts()
    {
        var store = SensorTestData.CreateStore();
        store.Update("asus-astral", CreateSample(CreateAlert()), SensorTestData.Timestamp);
        TestAssert.Equal(1, store.Snapshot(SensorTestData.Timestamp, []).Alerts.Length);
        TestAssert.Equal(0, store.Snapshot(SensorTestData.Timestamp.AddSeconds(6), []).Alerts.Length);
        store.ReportError("asus-astral", "Lost device");
        TestAssert.Equal(0, store.Snapshot(SensorTestData.Timestamp, []).Alerts.Length);
    }

    private static void CopiesPluginOwnedAlertArrays()
    {
        var store = SensorTestData.CreateStore();
        var alert = CreateAlert();
        store.Update("asus-astral", CreateSample(alert), SensorTestData.Timestamp);
        alert.Pins[0] = new AlertPin(6, 99);
        TestAssert.Equal(1, store.Snapshot(SensorTestData.Timestamp, []).Alerts.Single().Pins.Single().Channel);
    }

    private static void RejectsInvalidAlertOwnershipAndPins()
    {
        var store = SensorTestData.CreateStore();
        var alert = CreateAlert();
        TestAssert.Throws<ArgumentException>(() => Update(alert with { PluginId = "wireview-pro2" }));
        TestAssert.Throws<ArgumentException>(() => Update(alert with { DeviceId = "unknown" }));
        TestAssert.Throws<ArgumentException>(() => Update(alert with { Pins = [new AlertPin(0, 10)] }));
        TestAssert.Throws<ArgumentException>(() => Update(alert with { Pins = [new AlertPin(1, double.NaN)] }));
        TestAssert.Throws<ArgumentException>(() => Update(alert with { Pins = [new AlertPin(1, 10), new AlertPin(1, 9)] }));
        TestAssert.Throws<ArgumentException>(() => Update(alert with { ReportedAt = SensorTestData.Timestamp.AddMinutes(1) }));
        return;
        void Update(SensorAlert value) => store.Update("asus-astral", CreateSample(value), SensorTestData.Timestamp);
    }
}

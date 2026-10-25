using Rtx5090Monitoring.Plugins.WireView.Contracts.Warnings;
using Rtx5090Monitoring.Plugins.Asus.Contracts.Warnings;
using Rtx5090Monitoring.Contracts.Alerts;
using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Contracts.Health;
using Rtx5090Monitoring.Contracts.Warnings;
using Rtx5090Monitoring.Tests.Fixtures;
using Rtx5090Monitoring.Tests.Infrastructure;
using Rtx5090Monitoring.Warnings;

namespace Rtx5090Monitoring.Tests.Core;

internal static class WarningEngineTests
{
    public static void Run(TestRunner runner)
    {
        runner.Run(DefaultsUseFiveSecondsWithoutRepetition);
        runner.Run(RepeatsByTimeAndNotByReadingChanges);
        runner.Run(RepeatWithoutInitialWaitsForTheInterval);
        runner.Run(RecoveryRearmsWithoutTheOldCooldown);
        runner.Run(BriefRecoveryDoesNotStartANewEpisode);
        runner.Run(UnavailableDataPausesWithoutClaimingRecovery);
        runner.Run(PinChangesKeepTheSameWarningEpisode);
        runner.Run(WireViewBitsHaveIndependentStableIdentities);
        runner.Run(GlobalMuteAndEnableApplyImmediately);
        runner.Run(OverridesAndIntervalChangesApplyLive);
        runner.Run(RejectsInvalidPolicyAndLeavesMsiWithoutInventedWarnings);
    }
    private static void DefaultsUseFiveSecondsWithoutRepetition()
    {
        var engine = new WarningEngine(WarningTestData.Registry());
        var config = WarningTestData.Configuration();
        TestAssert.Equal(5, config.Warnings.Defaults.RepeatIntervalSeconds);
        var first = engine.Evaluate(WarningTestData.Frame(0), config, WarningTestData.Start);
        var later = engine.Evaluate(WarningTestData.Frame(20, 90), config, WarningTestData.Start.AddSeconds(20));
        TestAssert.Equal(1L, first.Sequence);
        TestAssert.Equal(first.Sequence, later.Sequence);
        TestAssert.Equal(first.States.Single().EpisodeId, later.States.Single().EpisodeId);
    }
    private static void RepeatsByTimeAndNotByReadingChanges()
    {
        var engine = new WarningEngine(WarningTestData.Registry());
        var config = WarningTestData.Configuration(new WarningPolicy { RepeatWhileActive = true });
        TestAssert.Equal(1L, engine.Evaluate(WarningTestData.Frame(0), config, WarningTestData.Start).Sequence);
        TestAssert.Equal(1L, engine.Evaluate(WarningTestData.Frame(1, 82), config, WarningTestData.Start.AddSeconds(1)).Sequence);
        TestAssert.Equal(2L, engine.Evaluate(WarningTestData.Frame(5, 82), config, WarningTestData.Start.AddSeconds(5)).Sequence);
        TestAssert.Equal(3L, engine.Evaluate(WarningTestData.Frame(10, 82), config, WarningTestData.Start.AddSeconds(10)).Sequence);
    }
    private static void RepeatWithoutInitialWaitsForTheInterval()
    {
        var engine = new WarningEngine(WarningTestData.Registry());
        var config = WarningTestData.Configuration(new WarningPolicy { NotifyOnStart = false, RepeatWhileActive = true });
        TestAssert.Equal(0L, engine.Evaluate(WarningTestData.Frame(0), config, WarningTestData.Start).Sequence);
        TestAssert.Equal(1L, engine.Evaluate(WarningTestData.Frame(5), config, WarningTestData.Start.AddSeconds(5)).Sequence);
    }
    private static void RecoveryRearmsWithoutTheOldCooldown()
    {
        var engine = new WarningEngine(WarningTestData.Registry());
        var config = WarningTestData.Configuration();
        var first = engine.Evaluate(WarningTestData.Frame(0), config, WarningTestData.Start).States.Single();
        TestAssert.Equal(WarningPhase.Recovering, engine.Evaluate(WarningTestData.Frame(1, 79), config, WarningTestData.Start.AddSeconds(1)).States.Single().Phase);
        TestAssert.Equal(0, engine.Evaluate(WarningTestData.Frame(3, 79), config, WarningTestData.Start.AddSeconds(3)).States.Length);
        var again = engine.Evaluate(WarningTestData.Frame(4), config, WarningTestData.Start.AddSeconds(4));
        TestAssert.Equal(2L, again.Sequence);
        TestAssert.False(first.EpisodeId == again.States.Single().EpisodeId, "Confirmed recovery must create a new occurrence.");
    }
    private static void BriefRecoveryDoesNotStartANewEpisode()
    {
        var engine = new WarningEngine(WarningTestData.Registry());
        var config = WarningTestData.Configuration();
        var first = engine.Evaluate(WarningTestData.Frame(0), config, WarningTestData.Start).States.Single();
        engine.Evaluate(WarningTestData.Frame(1, 79), config, WarningTestData.Start.AddSeconds(1));
        var resumed = engine.Evaluate(WarningTestData.Frame(2), config, WarningTestData.Start.AddSeconds(2));
        TestAssert.Equal(first.EpisodeId, resumed.States.Single().EpisodeId);
        TestAssert.Equal(1L, resumed.Sequence);
    }
    private static void UnavailableDataPausesWithoutClaimingRecovery()
    {
        var engine = new WarningEngine(WarningTestData.Registry());
        var config = WarningTestData.Configuration(new WarningPolicy { RepeatWhileActive = true });
        var first = engine.Evaluate(WarningTestData.Frame(0), config, WarningTestData.Start).States.Single();
        var missing = WarningTestData.Frame(5) with { Sensors = [], Plugins = [] };
        var unavailable = engine.Evaluate(missing, config, missing.Timestamp);
        TestAssert.Equal(WarningPhase.Unavailable, unavailable.States.Single().Phase);
        TestAssert.Equal(1L, unavailable.Sequence);
        var resumed = engine.Evaluate(WarningTestData.Frame(20), config, WarningTestData.Start.AddSeconds(20));
        TestAssert.Equal(first.EpisodeId, resumed.States.Single().EpisodeId);
        TestAssert.Equal(2L, resumed.Sequence);
    }
    private static void PinChangesKeepTheSameWarningEpisode()
    {
        var engine = new WarningEngine(WarningTestData.Registry());
        var config = WarningTestData.Configuration();
        var first = engine.Evaluate(WarningTestData.Frame(0, 70, pins: [new AlertPin(1, 10)]), config, WarningTestData.Start);
        var changed = engine.Evaluate(WarningTestData.Frame(1, 70, pins: [new AlertPin(2, 11)]), config, WarningTestData.Start.AddSeconds(1));
        TestAssert.Equal(first.States.Single().Id, changed.States.Single().Id);
        TestAssert.Equal(first.States.Single().EpisodeId, changed.States.Single().EpisodeId);
        TestAssert.Equal(first.Sequence, changed.Sequence);
        TestAssert.True(changed.States.Single().Detail.Contains("P2", StringComparison.Ordinal), "Affected pins update details only.");
    }
    private static void WireViewBitsHaveIndependentStableIdentities()
    {
        var engine = new WarningEngine(WarningTestData.Registry());
        var config = WarningTestData.Configuration();
        var first = engine.Evaluate(WarningTestData.Frame(0, 70, 0x02), config, WarningTestData.Start);
        var combined = engine.Evaluate(WarningTestData.Frame(1, 70, 0x06), config, WarningTestData.Start.AddSeconds(1));
        TestAssert.Equal(2, combined.States.Length);
        TestAssert.Equal(first.States.Single().NotificationSequence, combined.States.Single(state => state.TypeId == WireViewWarningIds.Overtemperature).NotificationSequence);
        TestAssert.Equal(2L, combined.States.Single(state => state.TypeId == WireViewWarningIds.Overcurrent).NotificationSequence);
    }
    private static void GlobalMuteAndEnableApplyImmediately()
    {
        var engine = new WarningEngine(WarningTestData.Registry());
        var config = WarningTestData.Configuration();
        var muted = config with { Warnings = config.Warnings with { NotificationsEnabled = false } };
        TestAssert.Equal(0L, engine.Evaluate(WarningTestData.Frame(0), muted, WarningTestData.Start).Sequence);
        TestAssert.Equal(1L, engine.Evaluate(WarningTestData.Frame(1), config, WarningTestData.Start.AddSeconds(1)).Sequence);
    }
    private static void OverridesAndIntervalChangesApplyLive()
    {
        var engine = new WarningEngine(WarningTestData.Registry());
        var config = WarningTestData.Configuration(new WarningPolicy { RepeatWhileActive = true });
        config = config with { Warnings = config.Warnings with { Overrides = new Dictionary<string, WarningPolicy> { [AsusWarningIds.CoreTemperature] = new() { RepeatWhileActive = true, RepeatIntervalSeconds = 10 } } } };
        engine.Evaluate(WarningTestData.Frame(0), config, WarningTestData.Start);
        TestAssert.Equal(1L, engine.Evaluate(WarningTestData.Frame(5), config, WarningTestData.Start.AddSeconds(5)).Sequence);
        TestAssert.Equal(2L, engine.Evaluate(WarningTestData.Frame(10), config, WarningTestData.Start.AddSeconds(10)).Sequence);
        config = config with { Warnings = config.Warnings with { Overrides = [] } };
        TestAssert.Equal(3L, engine.Evaluate(WarningTestData.Frame(15), config, WarningTestData.Start.AddSeconds(15)).Sequence);
    }
    private static void RejectsInvalidPolicyAndLeavesMsiWithoutInventedWarnings()
    {
        TestAssert.False(new WarningPolicy { RepeatIntervalSeconds = 4 }.IsValid(), "The minimum interval must be five seconds.");
        TestAssert.False(new WarningOptions { Overrides = new Dictionary<string, WarningPolicy> { [" "] = new() } }.IsValid(), "Empty warning IDs must be rejected.");
        var frame = WarningTestData.Frame(0, 70);
        frame = frame with { Sensors = [.. frame.Sensors.Select(sensor => sensor with { Identity = sensor.Identity with { PluginId = "msi-psu" } })], Plugins = [new PluginHealth("msi-psu", PluginState.Ready, frame.Timestamp, null)] };
        TestAssert.Equal(0, new WarningEngine(WarningTestData.Registry()).Evaluate(frame, WarningTestData.Configuration(), frame.Timestamp).States.Length);
    }
}

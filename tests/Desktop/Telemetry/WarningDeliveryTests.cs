using Rtx5090Monitoring.Contracts.Discovery;
using Rtx5090Monitoring.Plugins.Asus.Contracts.Warnings;
using Rtx5090Monitoring.Contracts.Warnings;
using Rtx5090Monitoring.Presentation.Notifications;
using Rtx5090Monitoring.Presentation.Settings;
using Rtx5090Monitoring.Tests.Infrastructure;
using Rtx5090Monitoring.UI.Tests.Fixtures;

namespace Rtx5090Monitoring.UI.Tests.Telemetry;

internal static class WarningDeliveryTests
{
    public static void Run(TestRunner runner)
    {
        runner.Run(UsesServerSequencesInsteadOfReadingsOrCooldowns);
        runner.Run(AggregatesAndHonorsAvailabilityAndLocalMute);
        runner.Run(EditsAndRevertsSharedPolicyWithoutLosingOverrides);
        runner.Run(UsesDynamicPluginDefinitionsAndPreservesAbsentPolicies);
    }
    private static void UsesServerSequencesInsteadOfReadingsOrCooldowns()
    {
        var tracker = new WarningTransitionTracker();
        var now = DateTimeOffset.UtcNow;
        var state = new WarningState("asus/gpu0/core", "asus-astral", "gpu0", AsusWarningIds.CoreTemperature, "Core temperature", "81 °C",
            WarningPhase.Active, "episode1", now, new WarningPolicy { RepeatWhileActive = true }, 1, now);
        var frame = UiTestData.Frame() with { Warnings = new WarningSnapshot("server1", 1, true, [state]) };
        TestAssert.Equal(1, tracker.Evaluate(frame, now).Length);
        TestAssert.Equal(0, tracker.Evaluate(frame with { Warnings = frame.Warnings with { States = [state with { Detail = "82 °C" }] } }, now.AddSeconds(1)).Length);
        TestAssert.Equal(1, tracker.Evaluate(frame with { Warnings = frame.Warnings with { Sequence = 2, States = [state with { NotificationSequence = 2 }] } }, now.AddSeconds(5)).Length);
        TestAssert.Equal(1, tracker.Evaluate(frame with { Warnings = frame.Warnings with { Sequence = 3, States = [state with { EpisodeId = "episode2", NotificationSequence = 3 }] } }, now.AddSeconds(8)).Length);
        TestAssert.Equal(0, new WarningTransitionTracker().Evaluate(UiTestData.Frame(), now).Length);
    }
    private static void AggregatesAndHonorsAvailabilityAndLocalMute()
    {
        var now = DateTimeOffset.UtcNow;
        var state = new WarningState("first", "asus-astral", "gpu0", AsusWarningIds.CoreTemperature, "Core", "81 °C", WarningPhase.Active, "e", now, new WarningPolicy(), 1, now);
        var frame = UiTestData.Frame() with { Warnings = new WarningSnapshot("server", 1, true, [state, state with { Id = "second", Title = "Memory" }]) };
        var tracker = new WarningTransitionTracker();
        TestAssert.Equal("RTX Monitor - 2 warnings", tracker.Evaluate(frame, now).Single().Title);
        TestAssert.Equal(0, tracker.Evaluate(frame, now).Length);
        TestAssert.Equal(0, tracker.Evaluate(frame, now, new AppPreferences { WarningNotifications = false }).Length);
        TestAssert.Equal(1, tracker.Evaluate(frame, now).Length);
        var unavailable = frame with { Warnings = frame.Warnings with { States = [state with { Phase = WarningPhase.Unavailable, NotificationSequence = 3 }] } };
        TestAssert.Equal(0, new WarningTransitionTracker().Evaluate(unavailable, now).Length);
    }
    private static void UsesDynamicPluginDefinitionsAndPreservesAbsentPolicies()
    {
        var model = new Rtx5090Monitoring.Presentation.Configuration.WarningOptionsEditorViewModel();
        model.Load(new WarningOptions { Overrides = new Dictionary<string, WarningPolicy> { ["vendor/custom"] = new() { RepeatWhileActive = true, RepeatIntervalSeconds = 11 } } });
        TestAssert.Equal(0, model.Overrides.Count);
        var plugin = UiTestData.Plugin with
        {
            Id = "vendor", Name = "Custom plugin", Enabled = false,
            Warnings = [new WarningDefinition("vendor/custom", "vendor", "Custom condition")]
        };
        model.SetCatalogue(new PluginCatalog(1, [plugin]));
        var custom = model.Overrides.Single();
        TestAssert.Equal("Custom plugin - Custom condition", custom.Name);
        TestAssert.False(custom.UseDefaults, "A disabled plugin must expose its saved override.");
        custom.Policy.RepeatIntervalSeconds = 15;
        model.Defaults.RepeatIntervalSeconds = double.NaN;
        model.SetCatalogue(new PluginCatalog(1, []));
        TestAssert.True(double.IsNaN(model.Defaults.RepeatIntervalSeconds), "Catalogue changes must preserve invalid drafts for correction.");
        model.Defaults.RepeatIntervalSeconds = 5;
        TestAssert.Equal(15, model.Build().Overrides["vendor/custom"].RepeatIntervalSeconds);
        model.SetCatalogue(new PluginCatalog(1, [plugin]));
        TestAssert.Equal(15d, model.Overrides.Single().Policy.RepeatIntervalSeconds);
        model.Overrides.Single().UseDefaults = true;
        TestAssert.False(model.Build().Overrides.ContainsKey("vendor/custom"), "Returning to defaults must remove only the selected override.");
    }
    private static void EditsAndRevertsSharedPolicyWithoutLosingOverrides()
    {
        var editor = new Rtx5090Monitoring.Presentation.Configuration.ConfigurationEditorViewModel(() => null!);
        editor.SetCatalog(new PluginCatalog(1, [UiTestData.Plugin]));
        editor.Receive(UiTestData.Configuration);
        editor.Warnings.Defaults.RepeatWhileActive = true;
        TestAssert.True(editor.IsDirty, "Changing reminder policy must enable Apply.");
        editor.Warnings.Defaults.RepeatWhileActive = false;
        TestAssert.False(editor.IsDirty, "Reverting reminder policy must clear the draft.");
        var special = editor.Warnings.Overrides.Single(item => item.TypeId == AsusWarningIds.CoreTemperature);
        special.UseDefaults = false;
        special.Policy.RepeatWhileActive = true;
        TestAssert.Equal(5, editor.Warnings.Build().Overrides[AsusWarningIds.CoreTemperature].RepeatIntervalSeconds);
        special.UseDefaults = true;
        TestAssert.False(editor.IsDirty, "Returning to inherited defaults must remove the override.");
    }
}

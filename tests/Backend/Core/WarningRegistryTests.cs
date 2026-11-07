using System.Text.Json;
using Rtx5090Monitoring.Plugins.GenericNvidia.Analysis.Warnings;
using Rtx5090Monitoring.Plugins.GenericNvidia.Contracts.Warnings;
using Microsoft.Extensions.Logging.Abstractions;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Contracts.Warnings;
using Rtx5090Monitoring.Core;
using Rtx5090Monitoring.Core.Plugins;
using Rtx5090Monitoring.Plugins.Asus.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.Asus.Contracts.Warnings;
using Rtx5090Monitoring.Service.Configuration;
using Rtx5090Monitoring.Tests.Fixtures;
using Rtx5090Monitoring.Tests.Infrastructure;
using Rtx5090Monitoring.Tests.TestDoubles;
using Rtx5090Monitoring.Warnings;

namespace Rtx5090Monitoring.Tests.Core;

internal static class WarningRegistryTests
{
    public static void Run(TestRunner runner)
    {
        runner.Run(AcceptsThirdPartyWarningsWithoutCoreChanges);
        runner.Run(DiscoversDefinitionsWithoutCreatingHardware);
        runner.Run(MigratesLegacyPoliciesAndPluginThresholds);
        runner.Run(RejectsDuplicateAndUndeclaredDefinitions);
        runner.Run(ValidatesThresholdsInsideThePlugin);
    }
    private static void AcceptsThirdPartyWarningsWithoutCoreChanges()
    {
        var source = new CustomWarningSource();
        var engine = new WarningEngine(new WarningRegistry([source]));
        var configuration = new ServiceConfiguration { Plugins = new Dictionary<string, PluginSettings> { [source.PluginId] = new() } };
        var frame = WarningTestData.Frame(0);
        var state = engine.Evaluate(frame, configuration, frame.Timestamp).States.Single();
        TestAssert.Equal("third-party/condition", state.TypeId);
        TestAssert.Equal("Custom condition", state.Title);
        TestAssert.Equal("temperature", state.Icon);
        TestAssert.Equal("Custom provider detail", state.Detail);
        TestAssert.Equal(1L, state.NotificationSequence);
        configuration = configuration with { Plugins = new Dictionary<string, PluginSettings>() };
        TestAssert.Equal(0, engine.Evaluate(frame, configuration, frame.Timestamp).States.Length);
    }
    private static void DiscoversDefinitionsWithoutCreatingHardware()
    {
        var factory = new WarningOnlyFactory();
        var runtime = new PluginRuntimeManager([factory], new SensorStateStore([]), TimeProvider.System, NullLogger<PluginRuntimeManager>.Instance);
        TestAssert.Equal("third-party/condition", runtime.Definitions.Single().Warnings.Single().Id);
        TestAssert.Equal(0, factory.Created);
        runtime.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
    private static void MigratesLegacyPoliciesAndPluginThresholds()
    {
        var configuration = WarningTestData.Configuration() with
        {
            Plugins = new Dictionary<string, PluginSettings> { ["generic-nvidia"] = new(), ["asus-astral"] = new() { Options = JsonSerializer.SerializeToElement(new AsusOptions()) } },
            Warnings = new WarningOptions
            {
                Overrides = new Dictionary<string, WarningPolicy> { ["AsusCoreTemperature"] = new() { RepeatWhileActive = true }, ["absent-plugin/future"] = new() { RepeatIntervalSeconds = 17 } },
                AdditionalSettings = new Dictionary<string, JsonElement> { ["coreTemperatureC"] = JsonSerializer.SerializeToElement(81), ["memoryTemperatureC"] = JsonSerializer.SerializeToElement(85) }
            }
        };
        var registry = WarningTestData.Registry();
        var upgraded = registry.Migrate(configuration);
        TestAssert.True(upgraded.Warnings.Overrides[GenericNvidiaWarningIds.CoreTemperature].RepeatWhileActive, "Legacy overrides must survive.");
        TestAssert.False(upgraded.Warnings.Overrides.ContainsKey("AsusCoreTemperature"), "Migration must replace only known legacy aliases.");
        TestAssert.Equal(17, upgraded.Warnings.Overrides["absent-plugin/future"].RepeatIntervalSeconds);
        var nvidia = upgraded.Plugins["generic-nvidia"].Options!.Value.Deserialize<Rtx5090Monitoring.Plugins.GenericNvidia.Contracts.Configuration.GenericNvidiaOptions>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        TestAssert.Equal<double?>(81, nvidia.CoreTemperatureWarningC);
        TestAssert.Equal<double?>(85, nvidia.MemoryTemperatureWarningC);
        var options = new AsusOptions();
        TestAssert.True(upgraded.Warnings.AdditionalSettings is null, "Migrated plugin settings must leave shared policy.");
        TestAssert.Equal(RuntimeJson.Serialize(upgraded), RuntimeJson.Serialize(registry.Migrate(upgraded)));
        TestAssert.True(configuration.Warnings.Overrides.ContainsKey("AsusCoreTemperature"), "Migration must not mutate a loaded baseline.");
        var explicitOptions = options with { CoreTemperatureWarningC = 90 };
        configuration = configuration with { Plugins = new Dictionary<string, PluginSettings> { ["asus-astral"] = new() { Options = JsonSerializer.SerializeToElement(explicitOptions, new JsonSerializerOptions(JsonSerializerDefaults.Web)) } } };
        var kept = registry.Migrate(configuration).Plugins["asus-astral"].Options!.Value;
        TestAssert.Equal(90d, kept.Deserialize<AsusOptions>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!.EffectiveCoreTemperatureWarningC);
    }
    private static void ValidatesThresholdsInsideThePlugin()
    {
        TestAssert.Throws<ArgumentException>(() => new AsusOptions { CoreTemperatureWarningC = double.NaN }.Validate());
        TestAssert.Throws<ArgumentException>(() => new AsusOptions { MemoryTemperatureWarningC = 121 }.Validate());
        new AsusOptions { CoreTemperatureWarningC = 81, MemoryTemperatureWarningC = 85 }.Validate();
    }
    private static void RejectsDuplicateAndUndeclaredDefinitions()
    {
        TestAssert.Throws<ArgumentException>(() => new WarningRegistry([new CustomWarningSource(), new CustomWarningSource()]));
        var source = new CustomWarningSource { Registered = false };
        var engine = new WarningEngine(new WarningRegistry([source]));
        var config = new ServiceConfiguration { Plugins = new Dictionary<string, PluginSettings> { [source.PluginId] = new() } };
        var frame = WarningTestData.Frame(0);
        TestAssert.Throws<InvalidOperationException>(() => engine.Evaluate(frame, config, frame.Timestamp));
    }
}

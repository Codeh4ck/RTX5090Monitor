using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Contracts.Health;
using Rtx5090Monitoring.Contracts.Sensors;
using Rtx5090Monitoring.Core.Configuration;
using Rtx5090Monitoring.Hardware.Nvidia;

using Rtx5090Monitoring.Service.Configuration;

using Rtx5090Monitoring.Tests.Fixtures;
using Rtx5090Monitoring.Tests.Infrastructure;
using Rtx5090Monitoring.Tests.TestDoubles;


namespace Rtx5090Monitoring.Tests.Service;

internal static class SettingsProfileTests
{
    public static void Run(TestRunner runner)
    {
        runner.Run(Detects5080ForWireViewWithoutAstralTelemetry);
        runner.Run(UsesLegacy5090ThresholdAndPreservesCustomAutoSettings);

        runner.Run(SavesNamedProfilesAndRestoresThemAfterRestart);
    }

    public static async Task RunEndpointsAsync(TestRunner runner)
    {
        await runner.RunAsync(LoadsAndAppliesSettingsProfilesThroughTheDesktopClientAsync);
    }

    private static async Task LoadsAndAppliesSettingsProfilesThroughTheDesktopClientAsync()
    {
        await using var host = await MonitoringTestHost.StartWithInstalledPluginsDisabledAsync();
        var api = new Client.Http.MonitoringApiClient(host.Client);
        var catalog = await api.GetSettingsProfilesAsync(CancellationToken.None);
        TestAssert.Equal("auto", catalog.SelectedId);
        var preset = catalog.Profiles.Single(profile => profile.Id == "rtx-5080");
        TestAssert.Equal(6d, preset.OvercurrentAmperes);
        var applied = await api.ApplySettingsProfileAsync(
            new SettingsProfileRequest(preset.Id, preset with { CurrentCeiling = 7 }, false), CancellationToken.None);
        TestAssert.Equal<double?>(7, applied.Effective?.CurrentCeiling);
        await api.ApplySettingsProfileAsync(new SettingsProfileRequest("auto", null, false), CancellationToken.None);
    }

    private static void Detects5080ForWireViewWithoutAstralTelemetry()
    {
        var snapshot = Frame("wireview-pro2", "com3", 0, 5, 5);
        var profile = Resolve("NVIDIA GeForce RTX 5080", snapshot);

        TestAssert.Equal("rtx-5080", profile.Profile.Id);
        TestAssert.Equal("com3", profile.WireViewDeviceId);
        TestAssert.Equal(6d, profile.Profile.CurrentCeiling);
        TestAssert.Equal(75d, profile.Profile.ResolvePowerCeiling(600));
    }

    private static void UsesLegacy5090ThresholdAndPreservesCustomAutoSettings()
    {
        var snapshot = Frame("asus-astral", "gpu0", 0, 8, 8);
        var active = Resolve("NVIDIA GeForce RTX 5090", snapshot);
        TestAssert.Equal(9.2, active.Profile.OvercurrentAmperes);
        TestAssert.Equal(10d, active.Profile.CurrentCeiling);

        var configuration = WarningTestData.Configuration();
        var previous = configuration.Plugins["asus-astral"];
        configuration.Plugins["asus-astral"] = Plugins.Asus.Configuration.AsusSettings.WithOptions(previous,
            new Rtx5090Monitoring.Plugins.Asus.Contracts.Configuration.AsusOptions { WireOvercurrentAmperageThreshold = 8 });
        TestAssert.Equal(8d, Resolve("NVIDIA GeForce RTX 5080", snapshot, configuration).Profile.OvercurrentAmperes);
    }

    private static void SavesNamedProfilesAndRestoresThemAfterRestart()
    {
        var path = Path.Combine(Path.GetTempPath(), "rtx-monitor-profile-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var options = StoreConfiguration(path);
            var store = new SettingsProfileStore(options, NullLogger<SettingsProfileStore>.Instance);
            var custom = SettingsProfile.Rtx5080 with { Name = "My Astral", CurrentCeiling = 7 };
            store.Apply(new SettingsProfileRequest("rtx-5080", custom, false));
            TestAssert.False(Directory.Exists(path), "Applying session edits must not create profile files.");
            store.Apply(new SettingsProfileRequest("rtx-5080", custom, true));
            var restored = new SettingsProfileStore(options, NullLogger<SettingsProfileStore>.Instance).Selection().Profile;
            TestAssert.Equal("My Astral", restored?.Name);
            TestAssert.Equal<double?>(7, restored?.CurrentCeiling);
            TestAssert.Equal(1, Directory.GetFiles(path, "*.profile.json").Length);
        }
        finally
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }

    private static ActiveSettingsProfile Resolve(string name, SensorSnapshot snapshot, ServiceConfiguration? configuration = null)
    {
        var hardware = new TestNvidiaHardwareSource { Devices = [new("gpu0", "/gpu-nvidia/0", name)] };
        using var services = new ServiceCollection()
            .AddSingleton(_ => new NvidiaTelemetryReader(hardware, TimeProvider.System)).BuildServiceProvider();
        var store = new SettingsProfileStore(StoreConfiguration(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))),
            NullLogger<SettingsProfileStore>.Instance);
        var resolver = new SettingsProfileResolver(store, new ActiveSettingsProfileState(), services, NullLogger<SettingsProfileResolver>.Instance);
        return resolver.Resolve(snapshot, configuration ?? WarningTestData.Configuration())
            ?? throw new InvalidOperationException("The configured GPU was not recognized.");
    }

    private static IConfiguration StoreConfiguration(string path) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["SettingsProfiles:Directory"] = path }).Build();

    private static SensorSnapshot Frame(string pluginId, string deviceId, int seconds, double high, double low)
    {
        var now = WarningTestData.Start.AddSeconds(seconds);
        return new SensorSnapshot(1, seconds + 1, now,
            [.. Enumerable.Range(1, 6).Select(pin => new SensorReading(new SensorIdentity(pluginId, deviceId,
                SensorComponent.GpuPin, SensorMetric.Current, pin), SensorUnit.Amperes, pin == 1 ? high : low, now))],
            [new PluginHealth(pluginId, PluginState.Ready, now, null)], [])
        {
            SettingsProfile = new(SettingsProfile.Rtx5080, "gpu0", pluginId == "wireview-pro2" ? deviceId : null)
        };
    }
}

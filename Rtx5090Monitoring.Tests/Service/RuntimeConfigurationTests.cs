using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Contracts.Sensors;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Contracts.Monitoring;
using Rtx5090Monitoring.Service.Configuration;
using Rtx5090Monitoring.Tests.Infrastructure;
using Rtx5090Monitoring.Tests.TestDoubles;

namespace Rtx5090Monitoring.Tests.Service;

internal static class RuntimeConfigurationTests
{
    private const string PluginId = "wireview-pro2";

    public static async Task RunAsync(TestRunner runner)
    {
        await runner.RunAsync(ReschedulesPollingWithoutWaitingForTheOldIntervalAsync);
        await runner.RunAsync(DisablesAndReenablesPluginsWithoutClosingTheSharedStreamAsync);
        await runner.RunAsync(ReschedulesPublicationWithoutWaitingForTheOldIntervalAsync);
        await runner.RunAsync(AppliesSourceSelectionsToTheNextFrameAsync);
        await runner.RunAsync(ReloadsAnExternalAtomicFileReplacementAsync);
        await runner.RunAsync(RetainsValidSettingsWhenTheFileIsInvalidAsync);
        await runner.RunAsync(RejectsInvalidApiSettingsWithoutChangingTheFileAsync);
        await runner.RunAsync(RollsBackAnActivationFailureAsync);
        await runner.RunAsync(DoesNotWaitForUnchangedHangingPluginsAsync);
        await runner.RunAsync(ReleasesResourcesAcrossRepeatedEnableDisableCyclesAsync);
        await runner.RunAsync(RejectsInvalidExternalSettingsWithoutStoppingThePluginAsync);
    }

    private static async Task ReschedulesPollingWithoutWaitingForTheOldIntervalAsync()
    {
        var device = new WireViewTestState();
        await using var host = await MonitoringTestHost.StartWithWireViewAsync(device, initialPollMs: 30000);
        await host.GetSensorSnapshotAsync();
        var initialPollCount = Volatile.Read(ref device.PollCount);

        using var response = await host.Client.PostAsJsonAsync($"/plugins/{PluginId}/config", new { pollIntervalMs = 100 });
        response.EnsureSuccessStatusCode();
        await TestWait.UntilAsync(() => Task.FromResult(Volatile.Read(ref device.PollCount) > initialPollCount), "the new polling interval");

        var current = await host.GetConfigurationAsync();
        var persisted = RuntimeJson.Deserialize(await File.ReadAllTextAsync(host.ConfigurationPath));
        TestAssert.Equal(100, current.Configuration.Plugins[PluginId].PollIntervalMs);
        TestAssert.Equal(100, persisted.Plugins[PluginId].PollIntervalMs);
        TestAssert.Equal(1, device.CreateCount);
        TestAssert.Equal(0, device.DisposeCount);
    }

    private static async Task DisablesAndReenablesPluginsWithoutClosingTheSharedStreamAsync()
    {
        var device = new WireViewTestState();
        await using var host = await MonitoringTestHost.StartWithWireViewAsync(device);
        var initialHealth = await host.GetHealthAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        using var client = await host.ConnectSensorClientAsync(timeout.Token);
        await WebSocketMessageReader.ReadUntilAsync(client, frame => frame.GetProperty("sensors").GetArrayLength() > 0, timeout.Token);

        var disabled = new PluginSettings { Enabled = false, PollIntervalMs = 100 };
        var disabledRevision = await host.SetPluginSettingsAsync(PluginId, disabled);
        var disabledFrame = await WebSocketMessageReader.ReadUntilAsync(client,
            frame => frame.GetProperty("configurationRevision").GetInt64() >= disabledRevision, timeout.Token);

        TestAssert.Equal(0, disabledFrame.GetProperty("sensors").GetArrayLength());
        TestAssert.Equal("Disabled", disabledFrame.GetProperty("plugins")[0].GetProperty("state").GetString());
        TestAssert.Equal(1, device.DisposeCount);

        await host.SetPluginSettingsAsync(PluginId, disabled with { Enabled = true });
        await WebSocketMessageReader.ReadUntilAsync(client, frame => frame.GetProperty("sensors").GetArrayLength() > 0, timeout.Token);
        var finalHealth = await host.GetHealthAsync();

        TestAssert.Equal(initialHealth.GetProperty("startedAt").GetString(), finalHealth.GetProperty("startedAt").GetString());
        TestAssert.Equal(2, device.CreateCount);
        TestAssert.Equal(WebSocketState.Open, client.State);
    }

    private static async Task ReschedulesPublicationWithoutWaitingForTheOldIntervalAsync()
    {
        await using var host = await MonitoringTestHost.StartWithoutPluginsAsync();
        var initial = await host.GetConfigurationAsync();
        await host.SetConfigurationAsync(initial.Configuration with { Monitoring = new MonitoringOptions { StreamIntervalMs = 30000 } });
        var slow = await host.GetConfigurationAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var client = await host.ConnectSensorClientAsync(timeout.Token);
        await WebSocketMessageReader.ReadUntilAsync(client,
            frame => frame.GetProperty("configurationRevision").GetInt64() == slow.Revision, timeout.Token);

        await host.SetConfigurationAsync(slow.Configuration with { Monitoring = new MonitoringOptions { StreamIntervalMs = 100 } });
        var fast = await host.GetConfigurationAsync();
        await WebSocketMessageReader.ReadUntilAsync(client,
            frame => frame.GetProperty("configurationRevision").GetInt64() == fast.Revision, timeout.Token);
    }

    private static async Task AppliesSourceSelectionsToTheNextFrameAsync()
    {
        await using var host = await MonitoringTestHost.StartWithWireViewAsync(new WireViewTestState());
        var current = await host.GetConfigurationAsync();
        var identity = new SensorIdentity(PluginId, "com3", SensorComponent.GpuConnector, SensorMetric.Power, Channel: 1);
        var selection = new SensorSelection { Name = "selected-power", PreferredSources = [identity] };
        var options = new MonitoringOptions { StreamIntervalMs = 100, Selections = [selection] };

        await host.SetConfigurationAsync(current.Configuration with { Monitoring = options });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var client = await host.ConnectSensorClientAsync(timeout.Token);
        var frame = await WebSocketMessageReader.ReadUntilAsync(client,
            snapshot => snapshot.GetProperty("selections").GetArrayLength() == 1, timeout.Token);

        TestAssert.Equal(identity.Key, frame.GetProperty("selections")[0].GetProperty("sensorKey").GetString());
    }

    private static async Task ReloadsAnExternalAtomicFileReplacementAsync()
    {
        var device = new WireViewTestState();
        await using var host = await MonitoringTestHost.StartWithWireViewAsync(device);
        var current = await host.GetConfigurationAsync();
        current.Configuration.Plugins[PluginId] = new PluginSettings { Enabled = false, PollIntervalMs = 200 };
        var replacement = host.ConfigurationPath + ".replacement";

        await File.WriteAllTextAsync(replacement, RuntimeJson.Serialize(current.Configuration));
        File.Move(replacement, host.ConfigurationPath, overwrite: true);
        await TestWait.UntilAsync(async () => (await host.GetConfigurationAsync()).Revision > current.Revision, "file reload");

        var updated = await host.GetConfigurationAsync();
        TestAssert.False(updated.Configuration.Plugins[PluginId].Enabled, "File edits must apply to plugin lifecycle.");
        TestAssert.Equal(1, device.DisposeCount);
    }

    private static async Task RetainsValidSettingsWhenTheFileIsInvalidAsync()
    {
        await using var host = await MonitoringTestHost.StartWithoutPluginsAsync();
        var originalContents = await File.ReadAllTextAsync(host.ConfigurationPath);
        var initial = await host.GetConfigurationAsync();

        await File.WriteAllTextAsync(host.ConfigurationPath, "{ invalid JSON");
        await TestWait.UntilAsync(async () => (await host.GetConfigurationAsync()).LastError is not null, "invalid-file error");
        TestAssert.Equal(initial.Revision, (await host.GetConfigurationAsync()).Revision);

        await File.WriteAllTextAsync(host.ConfigurationPath, originalContents);
        await TestWait.UntilAsync(async () => (await host.GetConfigurationAsync()).LastError is null, "recovery after repairing the file");
        TestAssert.Equal("running", (await host.GetHealthAsync()).GetProperty("status").GetString());
    }

    private static async Task RejectsInvalidApiSettingsWithoutChangingTheFileAsync()
    {
        await using var host = await MonitoringTestHost.StartWithWireViewAsync(new WireViewTestState());
        var before = await File.ReadAllTextAsync(host.ConfigurationPath);
        var settings = new PluginSettings { PollIntervalMs = 0 };

        using var response = await host.Client.PutAsJsonAsync($"/plugins/{PluginId}/settings", settings);

        TestAssert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        TestAssert.Equal(before, await File.ReadAllTextAsync(host.ConfigurationPath));
    }

    private static async Task RollsBackAnActivationFailureAsync()
    {
        var device = new WireViewTestState();
        await using var host = await MonitoringTestHost.StartWithWireViewAsync(device);
        var disabled = new PluginSettings { Enabled = false };
        await host.SetPluginSettingsAsync(PluginId, disabled);
        device.FailCreation = true;

        using var response = await host.Client.PutAsJsonAsync($"/plugins/{PluginId}/settings", disabled with { Enabled = true });

        TestAssert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var persisted = RuntimeJson.Deserialize(await File.ReadAllTextAsync(host.ConfigurationPath));
        TestAssert.False(persisted.Plugins[PluginId].Enabled, "Failed activation must roll back persisted settings.");
        TestAssert.False((await host.GetConfigurationAsync()).Configuration.Plugins[PluginId].Enabled, "Failed activation must retain effective settings.");
    }

    private static async Task ReleasesResourcesAcrossRepeatedEnableDisableCyclesAsync()
    {
        var device = new WireViewTestState();
        await using var host = await MonitoringTestHost.StartWithWireViewAsync(device);
        for (var cycle = 0; cycle < 3; cycle++)
        {
            await host.SetPluginSettingsAsync(PluginId, new PluginSettings { Enabled = false });
            await host.SetPluginSettingsAsync(PluginId, new PluginSettings { Enabled = true });
        }

        TestAssert.Equal(4, device.CreateCount);
        TestAssert.Equal(3, device.DisposeCount);
        TestAssert.Equal(0, device.ConcurrentAccessDetected);
    }

    private static async Task RejectsInvalidExternalSettingsWithoutStoppingThePluginAsync()
    {
        var device = new WireViewTestState();
        await using var host = await MonitoringTestHost.StartWithWireViewAsync(device);
        var current = await host.GetConfigurationAsync();
        current.Configuration.Plugins[PluginId] = new PluginSettings { PollIntervalMs = 0 };

        await File.WriteAllTextAsync(host.ConfigurationPath, RuntimeJson.Serialize(current.Configuration));
        await TestWait.UntilAsync(async () => (await host.GetConfigurationAsync()).LastError is not null, "invalid settings error");

        var effective = await host.GetConfigurationAsync();
        TestAssert.Equal(current.Revision, effective.Revision);
        TestAssert.Equal(1000, effective.Configuration.Plugins[PluginId].PollIntervalMs);
        TestAssert.Equal(0, device.DisposeCount);
    }

    private static async Task DoesNotWaitForUnchangedHangingPluginsAsync()
    {
        await using var host = await MonitoringTestHost.StartWithTestPluginsAsync();
        await host.GetSensorSnapshotAsync();
        var current = await host.GetConfigurationAsync();

        await host.SetConfigurationAsync(current.Configuration with { Monitoring = new MonitoringOptions { StreamIntervalMs = 200 } });

        TestAssert.Equal(200, (await host.GetConfigurationAsync()).Configuration.Monitoring.StreamIntervalMs);
    }
}

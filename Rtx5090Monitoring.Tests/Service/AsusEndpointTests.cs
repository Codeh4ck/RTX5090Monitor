using Rtx5090Monitoring.Plugins.Asus.Contracts.Configuration;
using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text.Json;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.Asus;
using Rtx5090Monitoring.Plugins.Asus.Configuration;
using Rtx5090Monitoring.Plugins.Asus.Mapping;
using Rtx5090Monitoring.Service.Configuration;
using Rtx5090Monitoring.Tests.Fixtures;
using Rtx5090Monitoring.Tests.Infrastructure;
using Rtx5090Monitoring.Tests.TestDoubles;

namespace Rtx5090Monitoring.Tests.Service;

internal static class AsusEndpointTests
{
    private const string Prefix = "/plugins/asus-astral";

    public static async Task RunAsync(TestRunner runner)
    {
        await runner.RunAsync(PreservesTheOriginalHttpAndWebSocketPayloadsAsync);
        await runner.RunAsync(PersistsAndAppliesTheOriginalConfigurationAsync);
        await runner.RunAsync(RejectsInvalidOptionsWithoutChangingTheFileAsync);
        await runner.RunAsync(SelectsOneGpuAndNeverFallsBackFromAnExplicitIdAsync);
        await runner.RunAsync(KeepsProducerIdentitiesSeparateFromWireViewAsync);
        await runner.RunAsync(DisablesAndReenablesWithoutInterruptingWireViewOrTheSharedStreamAsync);
        await runner.RunAsync(RestartsOnlyTheAsusPluginAsync);
        await runner.RunAsync(ReportsGpuFailureWithoutStoppingTheServiceAsync);
        await runner.RunAsync(ReloadsOptionsFromAnExternalFileEditAsync);
        await runner.RunAsync(ReschedulesPollingImmediatelyAsync);
        await runner.RunAsync(AddsAsusDefaultsToAnExistingWireViewConfigurationAsync);
        await runner.RunAsync(BindsPluginOwnedBootstrapOptionsAsync);
        await runner.RunAsync(StartsInstalledFactoriesDisabledWithoutTouchingHardwareAsync);
        await runner.RunAsync(RejectsInvalidExternalAsusOptionsWithoutChangingEffectiveSettingsAsync);
    }

    private static async Task PreservesTheOriginalHttpAndWebSocketPayloadsAsync()
    {
        await using var host = await MonitoringTestHost.StartWithAsusAsync(new AsusTestState());
        await host.GetSensorSnapshotAsync();
        using var document = JsonDocument.Parse(await host.Client.GetStringAsync(Prefix + "/sensors"));
        var snapshot = document.RootElement;

        TestAssert.Equal(4, snapshot.EnumerateObject().Count());
        TestAssert.Equal(32, snapshot.GetProperty("sensors").GetArrayLength());
        TestAssert.Equal(JsonValueKind.Null, snapshot.GetProperty("currentImbalance").ValueKind);
        TestAssert.Equal(JsonValueKind.Null, snapshot.GetProperty("wireOvercurrent").ValueKind);
        TestAssert.True(snapshot.GetProperty("sensors").EnumerateArray().Any(sensor =>
            sensor.GetProperty("sensorName").GetString() == "Pin 1" && sensor.GetProperty("sensorType").GetString() == "Current"),
            "Original friendly names and sensor type strings must survive the port.");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var client = await host.ConnectSensorClientAsync(timeout.Token, Prefix + "/ws");
        using var frame = await WebSocketMessageReader.ReadJsonAsync(client, timeout.Token);
        TestAssert.Equal(32, frame.RootElement.GetProperty("sensors").GetArrayLength());
        await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", timeout.Token);

        using var ordinaryGet = await host.Client.GetAsync(Prefix + "/ws");
        TestAssert.Equal(HttpStatusCode.BadRequest, ordinaryGet.StatusCode);
    }

    private static async Task PersistsAndAppliesTheOriginalConfigurationAsync()
    {
        var device = new AsusTestState();
        await using var host = await MonitoringTestHost.StartWithAsusAsync(device);
        await host.GetSensorSnapshotAsync();
        var current = await host.GetConfigurationAsync();
        var selected = AsusSettings.WithOptions(current.Configuration.Plugins[AsusPlugin.PluginId],
            new AsusOptions { DeviceId = AsusTestData.SecondDeviceId });
        await host.SetPluginSettingsAsync(AsusPlugin.PluginId, selected);

        var request = AsusConfigMapper.ToResponse(AsusSettings.WithOptions(selected with { PollIntervalMs = 100 }, new AsusOptions
        {
            DeviceId = AsusTestData.SecondDeviceId,
            ImbalanceAmperageThreshold = 7,
            ImbalancePercentageThreshold = 40,
            ImbalanceDurationThresholdMs = 500,
            WireOvercurrentAmperageThreshold = 10,
            WireOvercurrentThresholdMs = 600
        }));
        using var response = await host.Client.PutAsJsonAsync(Prefix + "/config", request);
        response.EnsureSuccessStatusCode();
        using var actual = JsonDocument.Parse(await host.Client.GetStringAsync(Prefix + "/config"));
        TestAssert.Equal(6, actual.RootElement.EnumerateObject().Count());
        TestAssert.Equal(100, actual.RootElement.GetProperty("pollIntervalMs").GetInt32());
        TestAssert.Equal(7, actual.RootElement.GetProperty("imbalanceAmperageThreshold").GetInt32());
        TestAssert.Equal(600, actual.RootElement.GetProperty("wireOvercurrentThresholdMs").GetInt32());

        var persisted = RuntimeJson.Deserialize(await File.ReadAllTextAsync(host.ConfigurationPath));
        var options = AsusSettings.GetOptions(persisted.Plugins[AsusPlugin.PluginId]);
        TestAssert.Equal(40, options.ImbalancePercentageThreshold);
        TestAssert.Equal(AsusTestData.SecondDeviceId, options.DeviceId);
        TestAssert.Equal(1, device.CreateCount);
        TestAssert.Equal(0, device.DisposeCount);
    }

    private static async Task RejectsInvalidOptionsWithoutChangingTheFileAsync()
    {
        await using var host = await MonitoringTestHost.StartWithAsusAsync(new AsusTestState());
        var before = await File.ReadAllTextAsync(host.ConfigurationPath);
        var current = await host.GetConfigurationAsync();
        var settings = current.Configuration.Plugins[AsusPlugin.PluginId] with
        {
            Options = JsonSerializer.SerializeToElement(new { wireOvercurrentAmperageThreshold = 100 })
        };
        using var invalid = await host.Client.PutAsJsonAsync(Prefix + "/settings", settings);
        TestAssert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        TestAssert.Equal(before, await File.ReadAllTextAsync(host.ConfigurationPath));

        var unknown = settings with { Options = JsonSerializer.SerializeToElement(new { misspelledThreshold = 7 }) };
        using var typo = await host.Client.PutAsJsonAsync(Prefix + "/settings", unknown);
        TestAssert.Equal(HttpStatusCode.BadRequest, typo.StatusCode);
        TestAssert.Equal(before, await File.ReadAllTextAsync(host.ConfigurationPath));
    }

    private static async Task SelectsOneGpuAndNeverFallsBackFromAnExplicitIdAsync()
    {
        var device = new AsusTestState();
        await using var host = await MonitoringTestHost.StartWithAsusAsync(device);
        await host.GetSensorSnapshotAsync();
        using var devices = JsonDocument.Parse(await host.Client.GetStringAsync(Prefix + "/devices"));
        TestAssert.Equal(2, devices.RootElement.GetArrayLength());

        var current = await host.GetConfigurationAsync();
        var selected = AsusSettings.WithOptions(current.Configuration.Plugins[AsusPlugin.PluginId],
            new AsusOptions { DeviceId = AsusTestData.SecondDeviceId });
        await host.SetPluginSettingsAsync(AsusPlugin.PluginId, selected);
        await TestWait.UntilAsync(() => Task.FromResult(device.LastRequestedDeviceId == AsusTestData.SecondDeviceId), "GPU selection");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var client = await host.ConnectSensorClientAsync(timeout.Token);
        var snapshot = await WebSocketMessageReader.ReadUntilAsync(client, frame => frame.GetProperty("sensors").GetArrayLength() > 0
            && frame.GetProperty("sensors").EnumerateArray().All(sensor =>
                sensor.GetProperty("identity").GetProperty("deviceId").GetString() == AsusTestData.SecondDeviceId), timeout.Token);
        TestAssert.True(snapshot.GetProperty("sensors").EnumerateArray().All(sensor =>
            sensor.GetProperty("identity").GetProperty("deviceId").GetString() == AsusTestData.SecondDeviceId), "Only the selected GPU may be published.");
        await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", timeout.Token);

        var missing = AsusSettings.WithOptions(selected, new AsusOptions { DeviceId = "/gpu-nvidia/missing" });
        await host.SetPluginSettingsAsync(AsusPlugin.PluginId, missing);
        await TestWait.UntilAsync(() => Task.FromResult(device.LastRequestedDeviceId == "/gpu-nvidia/missing"), "unavailable GPU selection");
        using var unavailable = await host.Client.GetAsync(Prefix + "/sensors");
        TestAssert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
    }

    private static async Task KeepsProducerIdentitiesSeparateFromWireViewAsync()
    {
        await using var host = await MonitoringTestHost.StartWithAsusAsync(new AsusTestState(), new WireViewTestState());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var client = await host.ConnectSensorClientAsync(timeout.Token);
        var snapshot = await WebSocketMessageReader.ReadUntilAsync(client, frame => frame.GetProperty("plugins").EnumerateArray()
            .Where(plugin => plugin.GetProperty("pluginId").GetString() != "generic-nvidia").All(plugin => plugin.GetProperty("state").GetString() == "Ready"), timeout.Token);
        var readings = snapshot.GetProperty("sensors").EnumerateArray().ToArray();
        var connectorPower = readings.Where(sensor => sensor.GetProperty("identity").GetProperty("component").GetString() == "GpuConnector"
            && sensor.GetProperty("identity").GetProperty("metric").GetString() == "Power").ToArray();

        TestAssert.Equal(2, connectorPower.Length);
        TestAssert.Equal(2, connectorPower.Select(sensor => sensor.GetProperty("identity").GetProperty("pluginId").GetString()).Distinct().Count());
        TestAssert.Equal(readings.Length, readings.Select(sensor => sensor.GetProperty("identity").GetProperty("key").GetString()).Distinct().Count());
        await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", timeout.Token);
    }

    private static async Task DisablesAndReenablesWithoutInterruptingWireViewOrTheSharedStreamAsync()
    {
        var device = new AsusTestState();
        var wireView = new WireViewTestState();
        await using var host = await MonitoringTestHost.StartWithAsusAsync(device, wireView);
        await host.GetSensorSnapshotAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        using var shared = await host.ConnectSensorClientAsync(timeout.Token);
        using var legacy = await host.ConnectSensorClientAsync(timeout.Token, Prefix + "/ws");
        using var initial = await WebSocketMessageReader.ReadJsonAsync(legacy, timeout.Token);
        var current = await host.GetConfigurationAsync();
        var disabled = current.Configuration.Plugins[AsusPlugin.PluginId] with { Enabled = false };
        var revision = await host.SetPluginSettingsAsync(AsusPlugin.PluginId, disabled);
        var frame = await WebSocketMessageReader.ReadUntilAsync(shared,
            data => data.GetProperty("configurationRevision").GetInt64() >= revision, timeout.Token);

        TestAssert.True(frame.GetProperty("sensors").EnumerateArray().All(sensor =>
            sensor.GetProperty("identity").GetProperty("pluginId").GetString() == "wireview-pro2"), "WireView must continue streaming.");
        await WebSocketMessageReader.WaitForCloseAsync(legacy, timeout.Token);
        TestAssert.Equal<WebSocketCloseStatus?>(WebSocketCloseStatus.EndpointUnavailable, legacy.CloseStatus);
        using var configuration = await host.Client.GetAsync(Prefix + "/config");
        TestAssert.Equal(HttpStatusCode.OK, configuration.StatusCode);

        await host.SetPluginSettingsAsync(AsusPlugin.PluginId, disabled with { Enabled = true });
        await WebSocketMessageReader.ReadUntilAsync(shared, data => data.GetProperty("sensors").EnumerateArray().Any(sensor =>
            sensor.GetProperty("identity").GetProperty("pluginId").GetString() == AsusPlugin.PluginId), timeout.Token);
        TestAssert.Equal(2, device.CreateCount);
        TestAssert.Equal(1, device.DisposeCount);
        TestAssert.Equal(1, wireView.CreateCount);
        TestAssert.Equal(0, wireView.DisposeCount);
        TestAssert.Equal(0, device.ConcurrentAccessDetected);
        await shared.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", timeout.Token);
    }

    private static async Task RestartsOnlyTheAsusPluginAsync()
    {
        var device = new AsusTestState();
        var wireView = new WireViewTestState();
        await using var host = await MonitoringTestHost.StartWithAsusAsync(device, wireView);
        await host.GetSensorSnapshotAsync();
        var before = await host.GetHealthAsync();
        using var response = await host.Client.PostAsync(Prefix + "/restart", content: null);
        response.EnsureSuccessStatusCode();

        TestAssert.Equal(2, device.CreateCount);
        TestAssert.Equal(1, device.DisposeCount);
        TestAssert.Equal(1, wireView.CreateCount);
        TestAssert.Equal(0, host.RestartRequests);
        TestAssert.Equal(before.GetProperty("startedAt").GetString(), (await host.GetHealthAsync()).GetProperty("startedAt").GetString());
    }

    private static async Task ReportsGpuFailureWithoutStoppingTheServiceAsync()
    {
        await using var host = await MonitoringTestHost.StartWithAsusAsync(new AsusTestState { FailReads = true });
        using var response = await host.Client.GetAsync(Prefix + "/sensors");
        TestAssert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await TestWait.UntilAsync(async () => (await host.GetHealthAsync()).GetProperty("plugins")[0].GetProperty("state").GetString() == "Error",
            "GPU error health");
        TestAssert.Equal("running", (await host.GetHealthAsync()).GetProperty("status").GetString());
    }

    private static async Task ReloadsOptionsFromAnExternalFileEditAsync()
    {
        var device = new AsusTestState();
        await using var host = await MonitoringTestHost.StartWithAsusAsync(device);
        var current = await host.GetConfigurationAsync();
        current.Configuration.Plugins[AsusPlugin.PluginId] = AsusSettings.WithOptions(current.Configuration.Plugins[AsusPlugin.PluginId],
            new AsusOptions { DeviceId = AsusTestData.SecondDeviceId, WireOvercurrentAmperageThreshold = 10 });
        await File.WriteAllTextAsync(host.ConfigurationPath, RuntimeJson.Serialize(current.Configuration));
        await TestWait.UntilAsync(() => Task.FromResult(device.LastRequestedDeviceId == AsusTestData.SecondDeviceId), "external ASUS options reload");

        var updated = await host.GetConfigurationAsync();
        TestAssert.Equal(10f, AsusSettings.GetOptions(updated.Configuration.Plugins[AsusPlugin.PluginId]).WireOvercurrentAmperageThreshold);
        TestAssert.Equal(1, device.CreateCount);
    }

    private static async Task ReschedulesPollingImmediatelyAsync()
    {
        var device = new AsusTestState();
        await using var host = await MonitoringTestHost.StartWithAsusAsync(device);
        var current = await host.GetConfigurationAsync();
        var slow = AsusSettings.WithOptions(current.Configuration.Plugins[AsusPlugin.PluginId] with
        {
            PollIntervalMs = 30000,
            StaleAfterMs = 30000
        }, new AsusOptions { ImbalanceDurationThresholdMs = 30000, WireOvercurrentThresholdMs = 30000 });
        await host.SetPluginSettingsAsync(AsusPlugin.PluginId, slow);
        var before = Volatile.Read(ref device.PollCount);
        var fast = AsusSettings.WithOptions(slow with { PollIntervalMs = 100 }, new AsusOptions());
        await host.SetPluginSettingsAsync(AsusPlugin.PluginId, fast);
        await TestWait.UntilAsync(() => Task.FromResult(Volatile.Read(ref device.PollCount) > before), "ASUS polling interval wakeup");
        TestAssert.Equal(1, device.CreateCount);
    }

    private static async Task AddsAsusDefaultsToAnExistingWireViewConfigurationAsync()
    {
        var existing = new ServiceConfiguration
        {
            Plugins = new Dictionary<string, PluginSettings>
            {
                ["wireview-pro2"] = new() { Enabled = false, PollIntervalMs = 250 }
            }
        };
        var device = new AsusTestState();
        await using var host = await MonitoringTestHost.StartWithAsusAsync(device, new WireViewTestState(), existing);
        var persisted = RuntimeJson.Deserialize(await File.ReadAllTextAsync(host.ConfigurationPath));

        TestAssert.Equal(3, persisted.Plugins.Count);
        TestAssert.False(persisted.Plugins["wireview-pro2"].Enabled, "Existing enablement must survive deployment.");
        TestAssert.Equal(250, persisted.Plugins["wireview-pro2"].PollIntervalMs);
        TestAssert.True(persisted.Plugins[AsusPlugin.PluginId].Enabled, "The newly installed ASUS plugin is enabled by default.");
        TestAssert.Equal(9.2f, AsusSettings.GetOptions(persisted.Plugins[AsusPlugin.PluginId]).WireOvercurrentAmperageThreshold);
    }

    private static async Task BindsPluginOwnedBootstrapOptionsAsync()
    {
        var bootstrap = new Dictionary<string, string?>
        {
            ["Plugins:asus-astral:Options:DeviceId"] = AsusTestData.SecondDeviceId,
            ["Plugins:asus-astral:Options:WireOvercurrentAmperageThreshold"] = "10.1"
        };
        await using var host = await MonitoringTestHost.StartWithAsusAsync(new AsusTestState(), bootstrapOverrides: bootstrap);
        var configuration = await host.GetConfigurationAsync();
        var options = AsusSettings.GetOptions(configuration.Configuration.Plugins[AsusPlugin.PluginId]);

        TestAssert.Equal(AsusTestData.SecondDeviceId, options.DeviceId);
        TestAssert.Equal(10.1f, options.WireOvercurrentAmperageThreshold);
    }

    private static async Task StartsInstalledFactoriesDisabledWithoutTouchingHardwareAsync()
    {
        await using var host = await MonitoringTestHost.StartWithInstalledPluginsDisabledAsync();
        var configuration = await host.GetConfigurationAsync();
        var health = await host.GetHealthAsync();

        TestAssert.Equal(4, configuration.Configuration.Plugins.Count);
        TestAssert.True(health.GetProperty("plugins").EnumerateArray().All(plugin => plugin.GetProperty("state").GetString() == "Disabled"),
            "All installed factories must register without constructing hardware readers.");
        using var asus = await host.Client.GetAsync(Prefix + "/config");
        using var wireView = await host.Client.GetAsync("/plugins/wireview-pro2/config");
        TestAssert.Equal(HttpStatusCode.OK, asus.StatusCode);
        TestAssert.Equal(HttpStatusCode.OK, wireView.StatusCode);
    }

    private static async Task RejectsInvalidExternalAsusOptionsWithoutChangingEffectiveSettingsAsync()
    {
        var device = new AsusTestState();
        await using var host = await MonitoringTestHost.StartWithAsusAsync(device);
        var before = await File.ReadAllTextAsync(host.ConfigurationPath);
        var current = await host.GetConfigurationAsync();
        current.Configuration.Plugins[AsusPlugin.PluginId] = current.Configuration.Plugins[AsusPlugin.PluginId] with
        {
            Options = JsonSerializer.SerializeToElement(new { wireOvercurrentAmperageThreshold = 100 })
        };
        await File.WriteAllTextAsync(host.ConfigurationPath, RuntimeJson.Serialize(current.Configuration));
        await TestWait.UntilAsync(async () => (await host.GetConfigurationAsync()).LastError is not null, "ASUS validation error");
        var retained = await host.GetConfigurationAsync();

        TestAssert.Equal(current.Revision, retained.Revision);
        TestAssert.Equal(9.2f, AsusSettings.GetOptions(retained.Configuration.Plugins[AsusPlugin.PluginId]).WireOvercurrentAmperageThreshold);
        TestAssert.Equal(1, device.CreateCount);

        await File.WriteAllTextAsync(host.ConfigurationPath, before);
        await TestWait.UntilAsync(async () => (await host.GetConfigurationAsync()).LastError is null, "repaired ASUS settings");
    }
}

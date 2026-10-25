using System.Net;
using System.Net.WebSockets;
using System.Text.Json;
using Rtx5090Monitoring.Tests.Infrastructure;

namespace Rtx5090Monitoring.Tests.Service;

internal static class SensorEndpointTests
{
    public static async Task RunAsync(TestRunner runner)
    {
        await runner.RunAsync(RequiresAWebSocketUpgradeAsync);

        var legacyPaths = new[] { "/ws", "/config", "/device/config", "/restart", "/faults/clear" };

        foreach (var path in legacyPaths)
        {
            await runner.RunAsync($"{nameof(SensorEndpointTests)}.DoesNotExposeLegacyEndpoint({path})",
                () => DoesNotExposeLegacyEndpointAsync(path));
        }

        await runner.RunAsync(StreamsVersionedEnvelopesAsync);
        await runner.RunAsync(StreamsHealthyPluginsWhileOtherPluginsFailOrWaitAsync);
        await runner.RunAsync(SerializesMetricsAsEnumNamesAsync);
        await runner.RunAsync(SerializesUnitsAsEnumNamesAsync);
        await runner.RunAsync(ClosesTheWebSocketCleanlyAsync);
        await runner.RunAsync(RejectsIncomingCommandsAsync);
        await runner.RunAsync(StreamsEmptySnapshotsWithoutPluginsAsync);
    }

    private static async Task RequiresAWebSocketUpgradeAsync()
    {
        await using var host = await MonitoringTestHost.StartWithoutPluginsAsync();

        using var response = await host.Client.GetAsync("/sensors");

        TestAssert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task DoesNotExposeLegacyEndpointAsync(string path)
    {
        await using var host = await MonitoringTestHost.StartWithoutPluginsAsync();

        using var response = await host.Client.GetAsync(path);

        var expected = path == "/restart" ? HttpStatusCode.MethodNotAllowed : HttpStatusCode.NotFound;
        TestAssert.Equal(expected, response.StatusCode);
    }

    private static async Task StreamsVersionedEnvelopesAsync()
    {
        await using var host = await MonitoringTestHost.StartWithTestPluginsAsync();

        var snapshot = await host.GetSensorSnapshotAsync();
        var schemaVersion = snapshot.GetProperty("schemaVersion").GetInt32();

        TestAssert.Equal(1, schemaVersion);
    }

    private static async Task StreamsHealthyPluginsWhileOtherPluginsFailOrWaitAsync()
    {
        await using var host = await MonitoringTestHost.StartWithTestPluginsAsync();

        var snapshot = await host.GetSensorSnapshotAsync();
        var reading = snapshot.GetProperty("sensors")[0];
        var pluginId = reading.GetProperty("identity").GetProperty("pluginId").GetString();

        TestAssert.Equal("healthy", pluginId);
    }

    private static async Task SerializesMetricsAsEnumNamesAsync()
    {
        await using var host = await MonitoringTestHost.StartWithTestPluginsAsync();

        var snapshot = await host.GetSensorSnapshotAsync();
        var reading = snapshot.GetProperty("sensors")[0];
        var metric = reading.GetProperty("identity").GetProperty("metric");

        TestAssert.Equal(JsonValueKind.String, metric.ValueKind);
    }

    private static async Task SerializesUnitsAsEnumNamesAsync()
    {
        await using var host = await MonitoringTestHost.StartWithTestPluginsAsync();

        var snapshot = await host.GetSensorSnapshotAsync();
        var reading = snapshot.GetProperty("sensors")[0];
        var unit = reading.GetProperty("unit");

        TestAssert.Equal(JsonValueKind.String, unit.ValueKind);
    }

    private static async Task ClosesTheWebSocketCleanlyAsync()
    {
        await using var host = await MonitoringTestHost.StartWithoutPluginsAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        using var client = await host.ConnectSensorClientAsync(timeout.Token);

        using var initialSnapshot = await WebSocketMessageReader.ReadJsonAsync(client, timeout.Token);
        await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Test complete", timeout.Token);

        TestAssert.Equal(WebSocketState.Closed, client.State);
    }

    private static async Task RejectsIncomingCommandsAsync()
    {
        await using var host = await MonitoringTestHost.StartWithoutPluginsAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        using var client = await host.ConnectSensorClientAsync(timeout.Token);

        using var initialSnapshot = await WebSocketMessageReader.ReadJsonAsync(client, timeout.Token);
        var command = "{}"u8.ToArray();

        await client.SendAsync(command.AsMemory(), WebSocketMessageType.Text, true, timeout.Token);
        await WebSocketMessageReader.WaitForCloseAsync(client, timeout.Token);

        TestAssert.Equal<WebSocketCloseStatus?>(WebSocketCloseStatus.PolicyViolation, client.CloseStatus);

        await client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Acknowledged", timeout.Token);
    }

    private static async Task StreamsEmptySnapshotsWithoutPluginsAsync()
    {
        await using var host = await MonitoringTestHost.StartWithoutPluginsAsync();

        var snapshot = await host.GetSensorSnapshotAsync(requireReadings: false);
        var sensorCount = snapshot.GetProperty("sensors").GetArrayLength();

        TestAssert.Equal(0, sensorCount);
    }
}

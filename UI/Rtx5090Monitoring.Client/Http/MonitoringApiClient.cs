using Rtx5090Monitoring.Plugins.GenericNvidia.Contracts.Devices;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Rtx5090Monitoring.Client.Serialization;
using Rtx5090Monitoring.Client.Streaming;
using Rtx5090Monitoring.Plugins.Asus.Contracts.Common;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Devices;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Contracts.Discovery;
using Rtx5090Monitoring.Contracts.Health;
using Rtx5090Monitoring.Contracts.Sensors;
using Rtx5090Monitoring.Plugins.WireView.Contracts.Common;
using Rtx5090Monitoring.Plugins.WireView.Contracts.Input;

namespace Rtx5090Monitoring.Client.Http;

public sealed class MonitoringApiClient(HttpClient http) : IMonitoringApi
{
    public Uri Address => http.BaseAddress ?? throw new InvalidOperationException("Service base address is required.");

    public async Task<Rtx5090Monitoring.Contracts.Updates.ServiceVersionResponse?> GetVersionAsync(CancellationToken token)
    {
        using var response = await http.GetAsync("version", token);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, token);
        var value = await response.Content.ReadFromJsonAsync(ClientJsonContext.Default.ServiceVersionResponse, token);

        return value is { SchemaVersion: 1 }
            && Rtx5090Monitoring.Contracts.Updates.SemanticVersion.TryParse(value.Version, out _) ? value : null;
    }

    public Task<HealthResponse> GetHealthAsync(CancellationToken token) => GetAsync("health", ClientJsonContext.Default.HealthResponse, token);
    public Task<PluginCatalog> GetPluginsAsync(CancellationToken token) => GetAsync("plugins", ClientJsonContext.Default.PluginCatalog, token);
    public async Task<ConfigurationSnapshot> GetConfigurationAsync(CancellationToken token) =>
        NormalizeConfiguration(await GetAsync("configuration", ClientJsonContext.Default.ConfigurationSnapshot, token));
    public Task<DeviceConfig> GetWireViewConfigurationAsync(CancellationToken token) =>
        GetAsync("plugins/wireview-pro2/device/config", ClientJsonContext.Default.DeviceConfig, token);
    public Task<AsusDeviceInfo[]> GetAsusDevicesAsync(CancellationToken token) =>
        GetAsync("plugins/asus-astral/devices", ClientJsonContext.Default.AsusDeviceInfoArray, token);
    public Task<NvidiaDeviceInfo[]> GetNvidiaDevicesAsync(CancellationToken token) =>
        GetAsync("plugins/generic-nvidia/devices", ClientJsonContext.Default.NvidiaDeviceInfoArray, token);

    public Task<MsiDeviceInfo[]> GetMsiDevicesAsync(CancellationToken token) =>
        GetAsync("plugins/msi-psu/devices", ClientJsonContext.Default.MsiDeviceInfoArray, token);

    public async Task<ConfigurationSnapshot> SetConfigurationAsync(ServiceConfiguration configuration, string version, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("Reload configuration before applying changes: its version is missing.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Put, "configuration")
        {
            Content = JsonContent.Create(configuration, ClientJsonContext.Default.ServiceConfiguration)
        };
        request.Headers.IfMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue($"\"{version}\""));
        using var response = await http.SendAsync(request, token);
        await EnsureSuccessAsync(response, token);
        return NormalizeConfiguration(await response.Content.ReadFromJsonAsync(ClientJsonContext.Default.ConfigurationSnapshot, token)
            ?? throw new InvalidDataException("The service returned empty configuration."));
    }

    private static ConfigurationSnapshot NormalizeConfiguration(ConfigurationSnapshot snapshot)
    {
        // Older services omit warnings. Source-generated record deserialization can assign null
        // to an absent init property instead of retaining its initializer.
        var warnings = snapshot.Configuration.Warnings ?? new Contracts.Warnings.WarningOptions();
        if (!warnings.IsValid())
        {
            throw new InvalidDataException("The service returned invalid warning settings.");
        }
        return snapshot with { Configuration = snapshot.Configuration with { Warnings = warnings } };
    }

    public async Task ClearWireViewFaultsAsync(ClearFaultsRequest request, CancellationToken token)
    {
        using var response = await http.PostAsJsonAsync("plugins/wireview-pro2/faults/clear", request, ClientJsonContext.Default.ClearFaultsRequest, token);
        await EnsureSuccessAsync(response, token);
    }

    public Task RestartPluginAsync(string pluginId, CancellationToken token) => PostAsync($"plugins/{Uri.EscapeDataString(pluginId)}/restart", token);
    public Task RestartServiceAsync(CancellationToken token) => PostAsync("restart", token);

    public async IAsyncEnumerable<SensorSnapshot> ReadSensorsAsync([EnumeratorCancellation] CancellationToken token)
    {
        using var socket = new ClientWebSocket();
        socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
        var address = new UriBuilder(new Uri(Address, "sensors")) { Scheme = Address.Scheme == "https" ? "wss" : "ws" }.Uri;
        await socket.ConnectAsync(address, token);
        await foreach (var frame in SensorFrameReader.ReadAsync(socket, token))
        {
            yield return frame;
        }
    }

    private async Task<T> GetAsync<T>(string path, JsonTypeInfo<T> type, CancellationToken token)
    {
        using var response = await http.GetAsync(path, token);
        await EnsureSuccessAsync(response, token);
        return await response.Content.ReadFromJsonAsync(type, token) ?? throw new InvalidDataException("The service returned an empty response.");
    }

    private async Task PostAsync(string path, CancellationToken token)
    {
        using var response = await http.PostAsync(path, null, token);
        await EnsureSuccessAsync(response, token);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken token)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var message = $"Service request failed: {(int)response.StatusCode} {response.ReasonPhrase}.";
        try
        {
            var error = await response.Content.ReadFromJsonAsync(ClientJsonContext.Default.ErrorResponse, token);
            if (!string.IsNullOrWhiteSpace(error?.Error))
            {
                message = error.Error;
            }
        }
        catch (JsonException)
        {
            // Older servers/proxies may return an HTML error page.
        }

        throw new MonitoringApiException(response.StatusCode, message);
    }
}

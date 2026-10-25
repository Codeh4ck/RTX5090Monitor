using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Rtx5090Monitoring.Client.Http;
using Rtx5090Monitoring.Plugins.Asus.Contracts.Common;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Contracts.Discovery;
using Rtx5090Monitoring.Contracts.Health;
using Rtx5090Monitoring.Contracts.Sensors;
using Rtx5090Monitoring.Plugins.WireView.Contracts.Common;
using Rtx5090Monitoring.Plugins.WireView.Contracts.Input;
using Rtx5090Monitoring.UI.Tests.Fixtures;

namespace Rtx5090Monitoring.UI.Tests.TestDoubles;

internal sealed class FakeMonitoringApi : IMonitoringApi
{
    public Task<Rtx5090Monitoring.Plugins.Msi.Contracts.Devices.MsiDeviceInfo[]> GetMsiDevicesAsync(CancellationToken token) =>
        Task.FromResult<Rtx5090Monitoring.Plugins.Msi.Contracts.Devices.MsiDeviceInfo[]>([]);
    public Uri Address { get; } = new("http://localhost:59273/");
    public ConfigurationSnapshot Configuration = UiTestData.Configuration;
    public Channel<SensorSnapshot> Frames { get; } = Channel.CreateUnbounded<SensorSnapshot>();
    public int StreamConnections;
    public int CatalogRequests;
    public int ConfigurationRequests;

    public Task<HealthResponse> GetHealthAsync(CancellationToken token) => Task.FromResult(new HealthResponse("ok", DateTimeOffset.UtcNow, 0, []));
    public Task<PluginCatalog> GetPluginsAsync(CancellationToken token)
    {
        Interlocked.Increment(ref CatalogRequests);
        return Task.FromResult(new PluginCatalog(1, [UiTestData.Plugin]));
    }
    public Task<ConfigurationSnapshot> GetConfigurationAsync(CancellationToken token)
    {
        Interlocked.Increment(ref ConfigurationRequests);
        return Task.FromResult(Configuration);
    }
    public Task<ConfigurationSnapshot> SetConfigurationAsync(ServiceConfiguration configuration, string version, CancellationToken token)
    {
        if (version != Configuration.Version)
        {
            throw new MonitoringApiException(HttpStatusCode.PreconditionFailed, "Configuration changed; reload.");
        }

        Configuration = new ConfigurationSnapshot(Configuration.Revision + 1, configuration, Version: "test:" + (Configuration.Revision + 1));
        return Task.FromResult(Configuration);
    }
    public Task<DeviceConfig> GetWireViewConfigurationAsync(CancellationToken token) => Task.FromResult(new DeviceConfig());
    public Task<AsusDeviceInfo[]> GetAsusDevicesAsync(CancellationToken token) => Task.FromResult<AsusDeviceInfo[]>([]);
    public Task ClearWireViewFaultsAsync(ClearFaultsRequest request, CancellationToken token) => Task.CompletedTask;
    public Task RestartPluginAsync(string pluginId, CancellationToken token) => Task.CompletedTask;
    public Task RestartServiceAsync(CancellationToken token) => Task.CompletedTask;
    public async IAsyncEnumerable<SensorSnapshot> ReadSensorsAsync([EnumeratorCancellation] CancellationToken token)
    {
        Interlocked.Increment(ref StreamConnections);
        await foreach (var frame in Frames.Reader.ReadAllAsync(token))
        {
            yield return frame;
        }
    }
}

using Rtx5090Monitoring.Plugins.Asus.Contracts.Common;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Devices;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Contracts.Discovery;
using Rtx5090Monitoring.Contracts.Health;
using Rtx5090Monitoring.Contracts.Sensors;
using Rtx5090Monitoring.Plugins.WireView.Contracts.Common;
using Rtx5090Monitoring.Plugins.WireView.Contracts.Input;

namespace Rtx5090Monitoring.Client.Http;

public interface IMonitoringApi
{
    Uri Address { get; }
    Task<HealthResponse> GetHealthAsync(CancellationToken token);
    Task<PluginCatalog> GetPluginsAsync(CancellationToken token);
    Task<ConfigurationSnapshot> GetConfigurationAsync(CancellationToken token);
    Task<ConfigurationSnapshot> SetConfigurationAsync(ServiceConfiguration configuration, string version, CancellationToken token);
    Task<DeviceConfig> GetWireViewConfigurationAsync(CancellationToken token);
    Task<AsusDeviceInfo[]> GetAsusDevicesAsync(CancellationToken token);
    Task<MsiDeviceInfo[]> GetMsiDevicesAsync(CancellationToken token);
    Task ClearWireViewFaultsAsync(ClearFaultsRequest request, CancellationToken token);
    Task RestartPluginAsync(string pluginId, CancellationToken token);
    Task RestartServiceAsync(CancellationToken token);
    IAsyncEnumerable<SensorSnapshot> ReadSensorsAsync(CancellationToken token);
}

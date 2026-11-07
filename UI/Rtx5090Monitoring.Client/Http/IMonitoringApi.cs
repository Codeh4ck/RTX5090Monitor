using Rtx5090Monitoring.Plugins.GenericNvidia.Contracts.Devices;
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
    Task<Rtx5090Monitoring.Contracts.Updates.ServiceVersionResponse?> GetVersionAsync(CancellationToken token);
    Task<HealthResponse> GetHealthAsync(CancellationToken token);
    Task<PluginCatalog> GetPluginsAsync(CancellationToken token);
    Task<SettingsProfileCatalog> GetSettingsProfilesAsync(CancellationToken token) =>
        Task.FromException<SettingsProfileCatalog>(new NotSupportedException("GPU settings profiles require a running monitoring service."));
    Task<SettingsProfileCatalog> ApplySettingsProfileAsync(SettingsProfileRequest request, CancellationToken token) =>
        Task.FromException<SettingsProfileCatalog>(new NotSupportedException("GPU settings profiles require a running monitoring service."));
    Task<DeviceWarningProfileCatalog> GetDeviceWarningProfilesAsync(CancellationToken token) =>
        Task.FromException<DeviceWarningProfileCatalog>(new NotSupportedException("Warning profiles require a running monitoring service."));
    Task SaveDeviceWarningProfileAsync(DeviceWarningProfile profile, CancellationToken token) =>
        Task.FromException(new NotSupportedException("Warning profiles require a running monitoring service."));
    Task DeleteDeviceWarningProfileAsync(string id, CancellationToken token) =>
        Task.FromException(new NotSupportedException("Warning profiles require a running monitoring service."));
    Task<WarningShutdownReason?> GetWarningShutdownAsync(CancellationToken token) => Task.FromResult<WarningShutdownReason?>(null);
    Task AcknowledgeWarningShutdownAsync(string id, CancellationToken token) => Task.CompletedTask;

    Task<ConfigurationSnapshot> GetConfigurationAsync(CancellationToken token);
    Task<ConfigurationSnapshot> SetConfigurationAsync(ServiceConfiguration configuration, string version, CancellationToken token);
    Task<DeviceConfig> GetWireViewConfigurationAsync(CancellationToken token);
    Task<AsusDeviceInfo[]> GetAsusDevicesAsync(CancellationToken token);
    Task<NvidiaDeviceInfo[]> GetNvidiaDevicesAsync(CancellationToken token) => Task.FromResult(Array.Empty<NvidiaDeviceInfo>());
    Task<MsiDeviceInfo[]> GetMsiDevicesAsync(CancellationToken token);
    Task<Rtx5090Monitoring.Plugins.Msi.Contracts.Telemetry.MsiSnapshot> GetMsiSnapshotAsync(CancellationToken token) =>
        Task.FromException<Rtx5090Monitoring.Plugins.Msi.Contracts.Telemetry.MsiSnapshot>(
            new NotSupportedException("MSI fan telemetry is unavailable from this connection."));
    Task ClearWireViewFaultsAsync(ClearFaultsRequest request, CancellationToken token);
    Task RestartPluginAsync(string pluginId, CancellationToken token);
    Task RestartServiceAsync(CancellationToken token);
    IAsyncEnumerable<SensorSnapshot> ReadSensorsAsync(CancellationToken token);
}

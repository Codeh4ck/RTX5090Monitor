using System.Runtime.CompilerServices;
using Rtx5090Monitoring.Client.Http;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Contracts.Discovery;
using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Contracts.Health;
using Rtx5090Monitoring.Contracts.Sensors;
using Rtx5090Monitoring.Debugging.Models;
using Rtx5090Monitoring.Debugging.Catalog;
using Rtx5090Monitoring.Plugins.Asus.Contracts.Common;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Devices;
using Rtx5090Monitoring.Plugins.WireView.Contracts.Common;
using Rtx5090Monitoring.Plugins.WireView.Contracts.Input;

namespace Rtx5090Monitoring.Debugging.Api;

public sealed class DebugMonitoringApi(DebugSession session, TimeProvider clock) : IMonitoringApi
{
    public Task<Rtx5090Monitoring.Contracts.Updates.ServiceVersionResponse?> GetVersionAsync(CancellationToken token) =>
        Task.FromResult<Rtx5090Monitoring.Contracts.Updates.ServiceVersionResponse?>(new(1, "1.0.0"));

    public Uri Address { get; } = new("http://debug.invalid/");
    private DebugFrame Current => session.Compose(clock.GetUtcNow());

    public Task<PluginCatalog> GetPluginsAsync(CancellationToken token) => Task.FromResult(Current.Catalog);

    public Task<ConfigurationSnapshot> GetConfigurationAsync(CancellationToken token) => Task.FromResult(Current.Configuration);

    public Task<HealthResponse> GetHealthAsync(CancellationToken token) => Task.FromResult(new HealthResponse("DEBUG", clock.GetUtcNow(), 0, Current.Snapshot.Plugins, 0));

    public Task<ConfigurationSnapshot> SetConfigurationAsync(ServiceConfiguration configuration, string version, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        session.SetConfiguration(configuration, version);
        return Task.FromResult(Current.Configuration);
    }

    public Task<DeviceConfig> GetWireViewConfigurationAsync(CancellationToken token) => Task.FromResult(DebugDeviceDefaults.WireView());
    public Task<Rtx5090Monitoring.Plugins.GenericNvidia.Contracts.Devices.NvidiaDeviceInfo[]> GetNvidiaDevicesAsync(CancellationToken token) =>
        Task.FromResult<Rtx5090Monitoring.Plugins.GenericNvidia.Contracts.Devices.NvidiaDeviceInfo[]>([new(session.Plugins.Single(plugin => plugin.Id == "generic-nvidia").DeviceId, "DEBUG GeForce RTX GPU", "debug-nvidia")]);

    public Task<AsusDeviceInfo[]> GetAsusDevicesAsync(CancellationToken token) => Task.FromResult<AsusDeviceInfo[]>([new AsusDeviceInfo(session.Plugins[1].DeviceId, "DEBUG ROG Astral")]);
    public Task<MsiDeviceInfo[]> GetMsiDevicesAsync(CancellationToken token) => Task.FromResult<MsiDeviceInfo[]>([new MsiDeviceInfo(session.Plugins[2].DeviceId, "DEBUG MPG TS PSU")]);

    public Task ClearWireViewFaultsAsync(ClearFaultsRequest request, CancellationToken token)
    {
        var plugin = session.Plugins[0];
        foreach (var metric in plugin.Metrics.Where(metric => metric.Definition.Metric is SensorMetric.FaultStatus or SensorMetric.FaultLog))
        {
            var mask = metric.Definition.Metric == SensorMetric.FaultStatus ? request.FaultStatusMask : request.FaultLogMask;
            if (mask == 0)
            {
                continue;
            }

            metric.Value = (ushort)metric.Value & ~mask;
            metric.ModeIndex = (int)DebugMetricMode.Fixed;
        }
        return Task.CompletedTask;
    }

    public Task RestartPluginAsync(string pluginId, CancellationToken token)
    {
        session.RestartAlerts();
        return Task.CompletedTask;
    }

    public Task RestartServiceAsync(CancellationToken token)
    {
        session.RestartAlerts();
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<SensorSnapshot> ReadSensorsAsync([EnumeratorCancellation] CancellationToken token)
    {
        while (session.Enabled && !token.IsCancellationRequested)
        {
            yield return Current.Snapshot;
            await Task.Delay(TimeSpan.FromSeconds(1), clock, token);
        }
    }
}

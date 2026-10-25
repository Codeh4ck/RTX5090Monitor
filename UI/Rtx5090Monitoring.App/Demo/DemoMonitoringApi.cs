using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Rtx5090Monitoring.Client.Http;
using Rtx5090Monitoring.Client.Serialization;
using Rtx5090Monitoring.Plugins.Asus.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Devices;
using Rtx5090Monitoring.Plugins.Asus.Contracts.Common;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Contracts.Discovery;
using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Contracts.Health;
using Rtx5090Monitoring.Contracts.Sensors;
using Rtx5090Monitoring.Plugins.WireView.Contracts.Common;
using Rtx5090Monitoring.Plugins.WireView.Contracts.Input;

namespace Rtx5090Monitoring.App.Demo;

internal sealed class DemoMonitoringApi : IMonitoringApi
{
    public Task<Rtx5090Monitoring.Contracts.Updates.ServiceVersionResponse?> GetVersionAsync(CancellationToken token) =>
        Task.FromResult<Rtx5090Monitoring.Contracts.Updates.ServiceVersionResponse?>(new(1, "1.0.0"));

    private readonly Lock _gate = new();
    private readonly DateTimeOffset _started = DateTimeOffset.UtcNow;
    private ConfigurationSnapshot _configuration = new(1, new ServiceConfiguration
    {
        Plugins = new Dictionary<string, PluginSettings>
        {
            ["wireview-pro2"] = new(),
            ["msi-psu"] = new() { Enabled = false, Options = JsonSerializer.SerializeToElement(new MsiOptions(), ClientJsonContext.Default.MsiOptions) },
            ["asus-astral"] = new() { Options = JsonSerializer.SerializeToElement(new AsusOptions(), ClientJsonContext.Default.AsusOptions) }
        }
    }, Version: "demo:1");
    private long _sequence;
    private readonly Warnings.WarningEngine _warnings = new(Rtx5090Monitoring.Debugging.Catalog.DebugWarningCatalog.Registry);
    private readonly HashSet<string> _unavailable = [];

    internal void SetDeviceAvailable(string pluginId, bool available)
    {
        lock (_gate)
        {
            if (available)
            {
                _unavailable.Remove(pluginId);
            }
            else
            {
                _unavailable.Add(pluginId);
            }
        }
    }

    public Uri Address { get; } = new("http://demo.invalid/");

    public Task<HealthResponse> GetHealthAsync(CancellationToken token) => Task.FromResult(new HealthResponse("ok", _started,
        (DateTimeOffset.UtcNow - _started).TotalSeconds, [], _configuration.Revision));

    public Task<PluginCatalog> GetPluginsAsync(CancellationToken token)
    {
        lock (_gate)
        {
            return Task.FromResult(new PluginCatalog(1, [
                .. _configuration.Configuration.Plugins.Select(pair => new PluginInfo(pair.Key,
                    pair.Key == "wireview-pro2" ? "WireView Pro 2" : pair.Key == "msi-psu" ? "MSI MPG Ai1300TS" : "ASUS ROG Astral",
                    pair.Value.Enabled,
                    pair.Value.Enabled ? PluginState.Ready : PluginState.Disabled, DateTimeOffset.UtcNow, "Simulated hardware",
                    pair.Key == "wireview-pro2"
                        ?
                        [
                            PluginCapability.Telemetry, PluginCapability.Configuration, PluginCapability.Restart,
                            PluginCapability.DeviceConfigurationRead, PluginCapability.FaultClear
                        ]
                        : [PluginCapability.Telemetry, PluginCapability.Configuration, PluginCapability.Restart, PluginCapability.DeviceSelection])
                    { Warnings = Rtx5090Monitoring.Debugging.Catalog.DebugWarningCatalog.ForPlugin(pair.Key) })
            ]));
        }
    }

    public Task<ConfigurationSnapshot> GetConfigurationAsync(CancellationToken token)
    {
        lock (_gate)
        {
            return Task.FromResult(_configuration);
        }
    }

    public Task<ConfigurationSnapshot> SetConfigurationAsync(ServiceConfiguration configuration, string version, CancellationToken token)
    {
        lock (_gate)
        {
            if (version != _configuration.Version)
            {
                throw new MonitoringApiException(HttpStatusCode.PreconditionFailed, "Demo configuration changed; reload before applying.");
            }

            var revision = _configuration.Revision + 1;
            _configuration = new ConfigurationSnapshot(revision, configuration, Version: "demo:" + revision);
            return Task.FromResult(_configuration);
        }
    }

    public Task<DeviceConfig> GetWireViewConfigurationAsync(CancellationToken token) => Task.FromResult(new DeviceConfig
    {
        Version = 2, FriendlyName = "WireView Pro 2 · DEMO", WireOcpFaultThresholdA = 9.2, OcpFaultThresholdA = 55,
        OppFaultThresholdW = 600, TsFaultThresholdC = 80, FanMode = "Automatic", FanTempSource = "Onboard", FanDutyMax = 100
    });

    public Task<AsusDeviceInfo[]> GetAsusDevicesAsync(CancellationToken token) => Task.FromResult<AsusDeviceInfo[]>([new AsusDeviceInfo("demo-gpu0", "ROG Astral RTX 5090 · DEMO")]);
    public Task<MsiDeviceInfo[]> GetMsiDevicesAsync(CancellationToken token) => Task.FromResult<MsiDeviceInfo[]>([new MsiDeviceInfo("demo-psu", "MPG Ai1300TS · DEMO")]);
    public Task ClearWireViewFaultsAsync(ClearFaultsRequest request, CancellationToken token) => Task.CompletedTask;
    public Task RestartPluginAsync(string pluginId, CancellationToken token) => Task.CompletedTask;
    public Task RestartServiceAsync(CancellationToken token) => Task.CompletedTask;

    public async IAsyncEnumerable<SensorSnapshot> ReadSensorsAsync([EnumeratorCancellation] CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            ConfigurationSnapshot configuration;
            HashSet<string> unavailable;
            lock (_gate)
            {
                configuration = _configuration;
                unavailable = [.. _unavailable];
            }

            var now = DateTimeOffset.UtcNow;
            var readings = new List<SensorReading>();
            var health = new List<PluginHealth>();
            var wave = Math.Sin((now - _started).TotalSeconds * 0.6);
            foreach (var (id, settings) in configuration.Configuration.Plugins)
            {
                var reporting = settings.Enabled && !unavailable.Contains(id);
                health.Add(new PluginHealth(id, !settings.Enabled ? PluginState.Disabled : reporting ? PluginState.Ready : PluginState.NoDevice,
                    reporting ? now : null, reporting ? "DEMO telemetry" : "DEMO device not connected"));
                if (!reporting)
                {
                    continue;
                }

                if (id == "msi-psu")
                {
                    for (var connector = 1; connector <= 2; connector++)
                    {
                        var sum = 0d;
                        for (var pin = 1; pin <= 6; pin++)
                        {
                            var current = connector == 1 ? 5.6 + pin * 0.1 + wave * 0.4 : 0;
                            sum += current;
                            readings.Add(new SensorReading(new SensorIdentity(id, "demo-psu", SensorComponent.PsuPin, SensorMetric.Current,
                                pin, connector.ToString()), SensorUnit.Amperes, current, now));
                        }
                        readings.Add(new SensorReading(new SensorIdentity(id, "demo-psu", SensorComponent.PsuConnector, SensorMetric.Current,
                            ConnectorId: connector.ToString()), SensorUnit.Amperes, sum, now));
                    }
                    var options = settings.Options?.Deserialize(ClientJsonContext.Default.MsiOptions) ?? new MsiOptions();
                    AddPsu(SensorMetric.Temperature, 48 + wave);
                    AddPsu(SensorMetric.FanSpeed, options.FanControl == MsiFanControl.Manual ? options.FanDutyPercent * 21 : 0, 1);
                    AddPsu(SensorMetric.FanDuty, options.FanControl == MsiFanControl.Manual ? options.FanDutyPercent : 0, 1);
                    AddPsu(SensorMetric.FanAutomatic, options.FanControl == MsiFanControl.Manual ? 0 : 1, 1);
                    continue;

                    void AddPsu(SensorMetric metric, double value, int? channel = null) => readings.Add(new SensorReading(
                        new SensorIdentity(id, "demo-psu", SensorComponent.Psu, metric, channel), SensorUnits.For(metric), value, now));
                }

                var device = id == "wireview-pro2" ? "demo-com3" : "demo-gpu0";
                var total = 0d;
                for (var pin = 1; pin <= 6; pin++)
                {
                    var current = 5.8 + pin * 0.09 + wave * 0.8;
                    total += current;
                    Add(SensorComponent.GpuPin, SensorMetric.Current, current, pin);
                    Add(SensorComponent.GpuPin, SensorMetric.Voltage, 12.02 - pin * 0.005, pin);
                    Add(SensorComponent.GpuPin, SensorMetric.Power, current * 12, pin);
                }
                Add(SensorComponent.GpuConnector, SensorMetric.Power, total * 12, 1);
                Add(SensorComponent.GpuConnector, SensorMetric.Current, total, 1);
                if (id == "wireview-pro2")
                {
                    Add(SensorComponent.MonitorBoard, SensorMetric.Temperature, 38 + wave, 1);
                    Add(SensorComponent.MonitorBoard, SensorMetric.Temperature, 41 + wave, 2);
                    Add(SensorComponent.ExternalProbe, SensorMetric.Temperature, 35, 1);
                    Add(SensorComponent.GpuConnector, SensorMetric.PowerCapability, 600, 1);
                    Add(SensorComponent.MonitorBoard, SensorMetric.FaultStatus, 0);
                    Add(SensorComponent.MonitorBoard, SensorMetric.FaultLog, 0);
                }
                else
                {
                    Add(SensorComponent.GpuCore, SensorMetric.Clock, 2780 + wave * 50);
                    Add(SensorComponent.GpuMemory, SensorMetric.Clock, 14000);
                    Add(SensorComponent.GpuCore, SensorMetric.Temperature, 67 + wave * 2);
                    Add(SensorComponent.GpuMemory, SensorMetric.Temperature, 76 + wave * 2);
                    Add(SensorComponent.GpuFan, SensorMetric.FanSpeed, 1450, 1);
                    Add(SensorComponent.GpuFan, SensorMetric.FanSpeed, 1480, 2);
                    Add(SensorComponent.GpuCore, SensorMetric.Utilization, 93 + wave * 2);
                    Add(SensorComponent.GpuMemory, SensorMetric.Utilization, 48);
                    Add(SensorComponent.GpuMemory, SensorMetric.MemoryUsed, 15900);
                    Add(SensorComponent.GpuMemory, SensorMetric.MemoryFree, 16868);
                    Add(SensorComponent.GpuMemory, SensorMetric.MemoryTotal, 32768);
                    Add(SensorComponent.GpuBoard, SensorMetric.Power, total * 12 + 38);
                }

                continue;

                void Add(SensorComponent component, SensorMetric metric, double value, int? channel = null) =>
                    readings.Add(new SensorReading(new SensorIdentity(id, device, component, metric, channel), SensorUnits.For(metric), value, now));
            }

            var snapshot = new SensorSnapshot(1, ++_sequence, now, [.. readings], [.. health], [], configuration.Revision);
            yield return snapshot with { Warnings = _warnings.Evaluate(snapshot, configuration.Configuration, now) };
            await Task.Delay(configuration.Configuration.Monitoring.StreamIntervalMs, token);
        }
    }
}

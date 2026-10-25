using Rtx5090Monitoring.Plugins.Asus.Contracts.Configuration;
using System.Text.Json;
using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Core.Plugins;
using Rtx5090Monitoring.Plugins.Asus.Configuration;
using Rtx5090Monitoring.Plugins.Asus.Contracts.Common;
using Rtx5090Monitoring.Plugins.Asus.Device;
using Rtx5090Monitoring.Plugins.Asus.Mapping;
using Rtx5090Monitoring.Plugins.Asus.Analysis.Mapping;
using Rtx5090Monitoring.Plugins.Asus.Analysis.Monitoring;
using Rtx5090Monitoring.Plugins.Asus.Serialization;
using Rtx5090Monitoring.Plugins.Asus.Streaming;

namespace Rtx5090Monitoring.Plugins.Asus;

public sealed class AsusPlugin(PluginSettings settings, TimeProvider clock, IAsusGpuReader reader, AsusStream stream) : ISensorPlugin
{
    public const string PluginId = "asus-astral";

    private readonly AsusAlertMonitor _alerts = new();
    private AsusOptions _options = AsusSettings.GetOptions(settings);
    private string? _deviceId;

    public PluginDescriptor Descriptor { get; private set; } = Describe(settings);
    public GpuSensorSnapshot? Latest { get; private set; }
    public IReadOnlyList<AsusDeviceInfo> Devices => reader.Devices;

    public void ApplySettings(PluginSettings settings)
    {
        var options = AsusSettings.GetOptions(settings);
        if (options != _options)
        {
            _alerts.Reset();
            if (options.DeviceId != _options.DeviceId)
            {
                ClearLatest();
                _deviceId = null;
            }
        }

        _options = options;
        Descriptor = Describe(settings);
    }

    public ValueTask<PluginSample> PollAsync(CancellationToken cancellationToken)
    {
        try
        {
            var data = reader.Read(_options.DeviceId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (data is null || data.Sensors.Length == 0)
            {
                ClearLatest();
                return ValueTask.FromResult(new PluginSample(PluginState.NoDevice, [], "Matching GPU or sensor data not available."));
            }

            if (_deviceId != data.DeviceId)
            {
                ClearLatest();
                _deviceId = data.DeviceId;
            }

            var now = clock.GetUtcNow();
            var currents = AsusSnapshotMapper.GetPinCurrents(data);
            _alerts.Evaluate(currents, _options, now);
            Latest = AsusSnapshotMapper.CreateLegacySnapshot(data, _alerts, now);
            var readings = AsusSnapshotMapper.CreateReadings(data, _alerts, currents, now);
            stream.Hub.Publish(JsonSerializer.SerializeToUtf8Bytes(Latest, AsusJsonContext.Default.GpuSensorSnapshot));
            return ValueTask.FromResult(new PluginSample(PluginState.Ready, readings, Alerts: AsusAlertMapper.Create(data.DeviceId, _alerts, now)));
        }
        catch
        {
            ClearLatest();
            throw;
        }
    }

    public void Dispose()
    {
        try
        {
            reader.Dispose();
        }
        finally
        {
            ClearLatest();
        }
    }

    private void ClearLatest()
    {
        Latest = null;
        _alerts.Reset();
        stream.Hub.Reset();
    }

    private static PluginDescriptor Describe(PluginSettings settings) => new(PluginId, "ASUS RTX GPU",
        TimeSpan.FromMilliseconds(settings.PollIntervalMs), TimeSpan.FromMilliseconds(settings.StaleAfterMs));
}

using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Core.Plugins;
using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Hardware.Nvidia;
using Rtx5090Monitoring.Plugins.GenericNvidia.Configuration;
using Rtx5090Monitoring.Plugins.GenericNvidia.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.GenericNvidia.Mapping;

namespace Rtx5090Monitoring.Plugins.GenericNvidia;

public sealed class GenericNvidiaPlugin(PluginSettings settings, NvidiaTelemetryReader reader) : ISensorPlugin
{
    public const string PluginId = "generic-nvidia";

    private GenericNvidiaOptions _options = GenericNvidiaSettings.GetOptions(settings);
    private int _pollIntervalMs = settings.PollIntervalMs;

    public PluginDescriptor Descriptor { get; private set; } = Describe(settings);

    public void ApplySettings(PluginSettings settings)
    {
        _options = GenericNvidiaSettings.GetOptions(settings);
        _pollIntervalMs = settings.PollIntervalMs;
        Descriptor = Describe(settings);
    }

    public ValueTask<PluginSample> PollAsync(CancellationToken cancellationToken)
    {
        var sample = reader.Read(_options.DeviceId, _pollIntervalMs, cancellationToken);
        var readings = sample is null ? [] : GenericNvidiaSnapshotMapper.Create(sample);

        return ValueTask.FromResult(readings.Length == 0
            ? new PluginSample(PluginState.NoDevice, [], "Selected desktop GeForce RTX GPU or supported telemetry is unavailable.")
            : new PluginSample(PluginState.Ready, readings, sample!.Device.Name));
    }

    public void Dispose()
    {
        // The host owns the reader shared with the ASUS plugin.
    }

    private static PluginDescriptor Describe(PluginSettings settings) => new(PluginId, "Generic NVIDIA GPU",
        TimeSpan.FromMilliseconds(settings.PollIntervalMs), TimeSpan.FromMilliseconds(settings.StaleAfterMs));
}

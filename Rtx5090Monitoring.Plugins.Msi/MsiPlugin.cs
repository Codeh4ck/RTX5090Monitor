using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Core.Plugins;
using Rtx5090Monitoring.Plugins.Msi.Configuration;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Devices;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Telemetry;
using Rtx5090Monitoring.Plugins.Msi.Device;
using Rtx5090Monitoring.Plugins.Msi.Mapping;

namespace Rtx5090Monitoring.Plugins.Msi;

public sealed class MsiPlugin(PluginSettings settings, TimeProvider clock, IMsiPsuReader reader) : ISensorPlugin
{
    public const string PluginId = "msi-psu";
    private MsiOptions _options = MsiSettings.GetOptions(settings);
    public PluginDescriptor Descriptor { get; private set; } = Describe(settings);
    public MsiSnapshot? Latest { get; private set; }
    public MsiDeviceInfo[] Devices => reader.Devices;

    public void ApplySettings(PluginSettings settings)
    {
        var options = MsiSettings.GetOptions(settings);
        if (_options.DevicePath != options.DevicePath || _options.FanControl != options.FanControl)
        {
            reader.ReleaseFanControl();
        }

        _options = options;
        Latest = null;
        Descriptor = Describe(settings);
    }

    public ValueTask<PluginSample> PollAsync(CancellationToken cancellationToken)
    {
        Latest = null;

        var sample = reader.Read(_options, clock.GetUtcNow(), cancellationToken);
        if (sample is null)
        {
            return ValueTask.FromResult(new PluginSample(PluginState.NoDevice, [],
                "No selected Ai1300TS or Ai1600TS is available. Select a device explicitly when more than one is connected."));
        }

        var readings = MsiSnapshotMapper.Create(sample);
        Latest = sample;

        return ValueTask.FromResult(new PluginSample(PluginState.Ready, readings, sample.Model + " · revision " + sample.Revision));
    }

    public void Dispose()
    {
        Latest = null;
        reader.Dispose();
    }

    private static PluginDescriptor Describe(PluginSettings settings) => new(PluginId, "MSI MPG Ai1300TS / Ai1600TS",
        TimeSpan.FromMilliseconds(settings.PollIntervalMs), TimeSpan.FromMilliseconds(settings.StaleAfterMs));
}
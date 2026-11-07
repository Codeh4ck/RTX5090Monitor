using LibreHardwareMonitor.Hardware;
using Rtx5090Monitoring.Hardware.Nvidia;
using Rtx5090Monitoring.Plugins.Asus.Contracts.Common;
using Rtx5090Monitoring.Plugins.Asus.Mapping;

namespace Rtx5090Monitoring.Plugins.Asus.Device;

public sealed class AsusGpuReader(NvidiaTelemetryReader reader) : IAsusGpuReader
{
    private int _pollIntervalMs = 1000;

    public IReadOnlyList<AsusDeviceInfo> Devices { get; private set; } = [];

    public void SetPollInterval(int milliseconds) => _pollIntervalMs = milliseconds;

    public AsusGpuData? Read(string? deviceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var devices = reader.GetDevices().Where(device => Matches(HardwareType.GpuNvidia, device.Name)).ToArray();
        devices = [.. devices.Where(device => IsAstral(reader.Read(device.Id, _pollIntervalMs, cancellationToken)))];

        Devices = [.. devices.Select(device => new AsusDeviceInfo(device.Id, device.Name))];

        var selected = deviceId is null ? devices.FirstOrDefault()
            : devices.FirstOrDefault(device => device.Id == deviceId || device.LegacyId == deviceId);

        if (selected is null || reader.Read(selected.Id, _pollIntervalMs, cancellationToken) is not { } sample)
        {
            return null;
        }

        var readings = new List<AsusSensorValue>();
        foreach (var sensor in sample.Sensors)
        {
            if (AsusSensorCatalog.Find(sensor.Name, sensor.Type) is { } definition)
            {
                readings.Add(new AsusSensorValue(definition, sensor.Value));
            }
        }

        return new AsusGpuData(selected.Id, selected.Name, [.. readings], sample.ObservedAt);
    }

    public static bool IsAstral(NvidiaSample? sample) => sample?.Sensors.Any(sensor => sensor is { Name: "12VHPWR Pin 1", Type: SensorType.Current }) == true;

    internal static bool Matches(HardwareType type, string name) => type == HardwareType.GpuNvidia
        && name.Contains("RTX", StringComparison.OrdinalIgnoreCase)
        && (name.Contains("5090", StringComparison.Ordinal) || name.Contains("5080", StringComparison.Ordinal));

    public void Dispose() => Devices = [];
}

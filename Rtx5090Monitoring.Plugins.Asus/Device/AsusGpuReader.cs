using Rtx5090Monitoring.Plugins.Asus.Contracts.Common;
using LibreHardwareMonitor.Hardware;
using Rtx5090Monitoring.Plugins.Asus.Mapping;

namespace Rtx5090Monitoring.Plugins.Asus.Device;

public sealed class AsusGpuReader : IAsusGpuReader
{
    private Computer? _computer;

    public IReadOnlyList<AsusDeviceInfo> Devices { get; private set; } = [];

    public AsusGpuData? Read(string? deviceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            if (_computer is null)
            {
                _computer = new Computer { IsGpuEnabled = true };
                _computer.Open();
            }

            var hardware = _computer.Hardware.Where(gpu => Matches(gpu.HardwareType, gpu.Name)).ToArray();
            Devices = [.. hardware.Select(gpu => new AsusDeviceInfo(gpu.Identifier.ToString(), gpu.Name))];
            var selected = deviceId is null ? hardware.FirstOrDefault()
                : hardware.FirstOrDefault(gpu => gpu.Identifier.ToString() == deviceId);

            if (selected is null)
            {
                CloseComputer();
                return null;
            }

            selected.Update();
            cancellationToken.ThrowIfCancellationRequested();
            var readings = new List<AsusSensorValue>();
            foreach (var sensor in selected.Sensors)
            {
                if (AsusSensorCatalog.Find(sensor.Name, sensor.SensorType) is { } definition)
                {
                    var value = sensor.Value;
                    readings.Add(new AsusSensorValue(definition, value is { } number && float.IsFinite(number) ? number : null));
                }
            }

            return new AsusGpuData(selected.Identifier.ToString(), selected.Name, [.. readings]);
        }
        catch
        {
            CloseComputer();
            Devices = [];
            throw;
        }
    }

    // Retain the original service's matching expression, including its 5080 branch.
    internal static bool Matches(HardwareType type, string name) => type == HardwareType.GpuNvidia
        && name.Contains("rtx", StringComparison.OrdinalIgnoreCase) && name.Contains("5090", StringComparison.OrdinalIgnoreCase)
        || name.Contains("5080", StringComparison.Ordinal);

    public void Dispose()
    {
        CloseComputer();
        Devices = [];
    }

    private void CloseComputer()
    {
        var computer = _computer;
        _computer = null;
        computer?.Close();
    }
}

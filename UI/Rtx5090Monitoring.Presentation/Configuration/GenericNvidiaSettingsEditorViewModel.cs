using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Rtx5090Monitoring.Plugins.GenericNvidia.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.GenericNvidia.Contracts.Devices;
using Rtx5090Monitoring.Presentation.Infrastructure;

namespace Rtx5090Monitoring.Presentation.Configuration;

public sealed class GenericNvidiaSettingsEditorViewModel : EditableObject
{
    private GpuChoice _selectedDevice;
    private bool _refreshing;
    private NvidiaDeviceInfo[] _devices = [];

    public ObservableCollection<GpuChoice> Devices { get; } = [];
    public double CoreTemperatureWarningC { get; set => Edit(ref field, value); }
    public double MemoryTemperatureWarningC { get; set => Edit(ref field, value); }
    public bool CoreWarningEnabled { get; set => Edit(ref field, value); }
    public bool MemoryWarningEnabled { get; set => Edit(ref field, value); }
    public int ElectricalSourceIndex { get; set => Edit(ref field, value); }

    [AllowNull]
    public GpuChoice SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (!_refreshing && value is not null)
            {
                Edit(ref _selectedDevice, value);
            }
        }
    }

    public GenericNvidiaSettingsEditorViewModel(GenericNvidiaOptions options)
    {
        CoreTemperatureWarningC = options.CoreTemperatureWarningC ?? 81;
        MemoryTemperatureWarningC = options.MemoryTemperatureWarningC ?? 85;
        CoreWarningEnabled = options.CoreTemperatureWarningC is not null;
        MemoryWarningEnabled = options.MemoryTemperatureWarningC is not null;
        ElectricalSourceIndex = options.ElectricalSource switch { "WireView" => 1, "None" => 2, _ => 0 };
        _selectedDevice = new GpuChoice(options.DeviceId, options.DeviceId ?? "Automatic - first desktop GeForce RTX GPU");
        SetDevices([]);
    }

    public void SetDevices(NvidiaDeviceInfo[] devices)
    {
        _devices = devices;
        var selectedId = _selectedDevice.Id;
        _refreshing = true;

        try
        {
            Devices.Clear();
            Devices.Add(new GpuChoice(null, "Automatic - first desktop GeForce RTX GPU"));
            foreach (var device in devices)
            {
                Devices.Add(new GpuChoice(device.DeviceId, device.HardwareName + " - " + device.DeviceId));
            }

            var selected = devices.FirstOrDefault(device => device.DeviceId == selectedId || device.LegacyDeviceId == selectedId)?.DeviceId ?? selectedId;
            if (selected is not null && Devices.All(device => device.Id != selected))
            {
                Devices.Add(new GpuChoice(selected, selected + " - currently unavailable"));
            }

            _selectedDevice = Devices.First(device => device.Id == selected);
            Raise(nameof(SelectedDevice));
        }
        finally
        {
            _refreshing = false;
        }
    }

    public GenericNvidiaOptions Build()
    {
        var options = new GenericNvidiaOptions
        {
            DeviceId = SelectedDevice.Id,
            CoreTemperatureWarningC = CoreWarningEnabled ? CoreTemperatureWarningC : null,
            MemoryTemperatureWarningC = MemoryWarningEnabled ? MemoryTemperatureWarningC : null,
            ElectricalSource = ElectricalSourceIndex switch { 0 => "Automatic", 1 => "WireView", 2 => "None", _ => throw new ArgumentException("Select an electrical source.") }
        };
        options.Validate();
        return options;
    }

    public bool Matches(GenericNvidiaOptions options) => (SelectedDevice.Id == options.DeviceId
        || _devices.Any(device => device.DeviceId == SelectedDevice.Id && device.LegacyDeviceId == options.DeviceId))
        && (CoreWarningEnabled ? CoreTemperatureWarningC == options.CoreTemperatureWarningC : options.CoreTemperatureWarningC is null)
        && (MemoryWarningEnabled ? MemoryTemperatureWarningC == options.MemoryTemperatureWarningC : options.MemoryTemperatureWarningC is null)
        && ElectricalSourceIndex == (options.ElectricalSource switch { "WireView" => 1, "None" => 2, _ => 0 });
}

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Rtx5090Monitoring.Plugins.Asus.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.Asus.Contracts.Common;
using Rtx5090Monitoring.Presentation.Infrastructure;

namespace Rtx5090Monitoring.Presentation.Configuration;

public sealed class AsusSettingsEditorViewModel : EditableObject
{
    private readonly AsusOptions _original;
    private GpuChoice _selectedDevice;
    private bool _refreshingDevices;

    public ObservableCollection<GpuChoice> Devices { get; } = [];

    [AllowNull]
    public GpuChoice SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            // ComboBox clears selection while its items are being refreshed.
            if (value is not null && !_refreshingDevices)
            {
                Edit(ref _selectedDevice, value);
            }
        }
    }

    public AsusSettingsEditorViewModel(AsusOptions options)
    {
        _original = options;
        _selectedDevice = options.DeviceId is null ? new GpuChoice(null, "Automatic · first matching GPU") : new GpuChoice(options.DeviceId, options.DeviceId);
        SetDevices([]);
    }

    public void SetDevices(AsusDeviceInfo[] devices)
    {
        var selectedId = _selectedDevice.Id;
        _refreshingDevices = true;
        try
        {
            Devices.Clear();
            Devices.Add(new GpuChoice(null, "Automatic · first matching GPU"));
            foreach (var device in devices)
            {
                Devices.Add(new GpuChoice(device.DeviceId, device.HardwareName + " · " + device.DeviceId));
            }

            if (selectedId is not null && Devices.All(device => device.Id != selectedId))
            {
                Devices.Add(new GpuChoice(selectedId, selectedId + " · currently unavailable"));
            }

            _selectedDevice = Devices.First(device => device.Id == selectedId);
            Raise(nameof(SelectedDevice));
        }
        finally
        {
            _refreshingDevices = false;
        }
    }

    public AsusOptions Build() => _original with { DeviceId = SelectedDevice.Id };

    public bool Matches(AsusOptions options) => Build() == options;
}

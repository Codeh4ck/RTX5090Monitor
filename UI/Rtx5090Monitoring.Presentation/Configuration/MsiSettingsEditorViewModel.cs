using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Devices;
using Rtx5090Monitoring.Presentation.Infrastructure;

namespace Rtx5090Monitoring.Presentation.Configuration;

public sealed class MsiSettingsEditorViewModel : EditableObject
{
    private PsuChoice _selectedDevice;
    private bool _refreshingDevices;
    private int _fanMode;
    private double _fanDuty;

    public ObservableCollection<PsuChoice> Devices { get; } = [];

    [AllowNull]
    public PsuChoice SelectedDevice
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

    public int FanModeIndex
    {
        get => _fanMode;
        set
        {
            if (value < 0)
            {
                return;
            }

            if (_fanMode == value)
            {
                return;
            }

            Edit(ref _fanMode, value);
            Raise(nameof(IsManual));
        }
    }

    public bool IsManual => FanModeIndex == (int)MsiFanControl.Manual;
    public double FanDutyPercent { get => _fanDuty; set => Edit(ref _fanDuty, value); }

    public MsiSettingsEditorViewModel(MsiOptions options)
    {
        _fanMode = (int)options.FanControl;
        _fanDuty = options.FanDutyPercent;
        _selectedDevice = new PsuChoice(options.DevicePath, options.DevicePath ?? "Automatic · single connected MPG TS PSU");
        SetDevices([]);
    }

    public void SetDevices(MsiDeviceInfo[] devices)
    {
        var id = _selectedDevice.Id;
        _refreshingDevices = true;
        try
        {
            Devices.Clear();
            Devices.Add(new PsuChoice(null, "Automatic · single connected MPG TS PSU"));
            for (var index = 0; index < devices.Length; index++)
            {
                Devices.Add(new PsuChoice(devices[index].DeviceId, devices[index].Name + " · device " + (index + 1)));
            }

            if (id is not null && Devices.All(device => device.Id != id))
            {
                Devices.Add(new PsuChoice(id, "Selected PSU · currently unavailable"));
            }

            _selectedDevice = Devices.First(device => device.Id == id);
            Raise(nameof(SelectedDevice));
        }
        finally
        {
            _refreshingDevices = false;
        }
    }

    public MsiOptions Build()
    {
        var options = new MsiOptions
        {
            DevicePath = SelectedDevice.Id,
            FanControl = (MsiFanControl)FanModeIndex,
            FanDutyPercent = NumericInput.Integer(FanDutyPercent, "MSI fan duty")
        };

        options.Validate();
        return options;
    }

    public bool Matches(MsiOptions options) => SelectedDevice.Id == options.DevicePath
                                               && FanModeIndex == (int)options.FanControl && Math.Abs(FanDutyPercent - options.FanDutyPercent) < 0.0001;
}
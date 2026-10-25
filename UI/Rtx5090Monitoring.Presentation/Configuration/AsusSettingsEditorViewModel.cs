using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Rtx5090Monitoring.Plugins.Asus.Contracts.Configuration;
using Rtx5090Monitoring.Plugins.Asus.Contracts.Common;
using Rtx5090Monitoring.Presentation.Infrastructure;

namespace Rtx5090Monitoring.Presentation.Configuration;

public sealed class AsusSettingsEditorViewModel : EditableObject
{
    private double _coreTemperature;
    private double _memoryTemperature;
    public double CoreTemperatureWarningC { get => _coreTemperature; set => Edit(ref _coreTemperature, value); }
    public double MemoryTemperatureWarningC { get => _memoryTemperature; set => Edit(ref _memoryTemperature, value); }
    private double _activation;
    private double _percentage;
    private double _imbalanceDuration;
    private double _overcurrent;
    private double _overcurrentDuration;
    private GpuChoice _selectedDevice;
    private bool _refreshingDevices;

    public ObservableCollection<GpuChoice> Devices { get; } = [];

    public double ImbalanceActivationA
    {
        get => _activation;
        set => Edit(ref _activation, value);
    }

    public double ImbalancePercentage
    {
        get => _percentage;
        set => Edit(ref _percentage, value);
    }

    public double ImbalanceDurationMs
    {
        get => _imbalanceDuration;
        set => Edit(ref _imbalanceDuration, value);
    }

    public double OvercurrentA
    {
        get => _overcurrent;
        set => Edit(ref _overcurrent, value);
    }

    public double OvercurrentDurationMs
    {
        get => _overcurrentDuration;
        set => Edit(ref _overcurrentDuration, value);
    }

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
        _coreTemperature = options.EffectiveCoreTemperatureWarningC;
        _memoryTemperature = options.EffectiveMemoryTemperatureWarningC;
        _activation = options.ImbalanceAmperageThreshold;
        _percentage = options.ImbalancePercentageThreshold;
        _imbalanceDuration = options.ImbalanceDurationThresholdMs;
        _overcurrent = options.WireOvercurrentAmperageThreshold;
        _overcurrentDuration = options.WireOvercurrentThresholdMs;
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

    public AsusOptions Build(int pollIntervalMs)
    {
        var options = new AsusOptions
        {
            CoreTemperatureWarningC = CoreTemperatureWarningC,
            MemoryTemperatureWarningC = MemoryTemperatureWarningC,
            DeviceId = SelectedDevice.Id,
            ImbalanceAmperageThreshold = NumericInput.Integer(ImbalanceActivationA, "Imbalance activation"),
            ImbalancePercentageThreshold = NumericInput.Integer(ImbalancePercentage, "Imbalance percentage"),
            ImbalanceDurationThresholdMs = NumericInput.Integer(ImbalanceDurationMs, "Imbalance duration"),
            WireOvercurrentAmperageThreshold = (float)OvercurrentA,
            WireOvercurrentThresholdMs = NumericInput.Integer(OvercurrentDurationMs, "Overcurrent duration")
        };
        options.Validate();
        var maximumDuration = Math.Max(pollIntervalMs, 10000);
        if (options.ImbalanceDurationThresholdMs < pollIntervalMs || options.ImbalanceDurationThresholdMs > maximumDuration
            || options.WireOvercurrentThresholdMs < pollIntervalMs || options.WireOvercurrentThresholdMs > maximumDuration)
        {
            throw new ArgumentException($"ASUS detection durations must be between {pollIntervalMs} and {maximumDuration} ms.");
        }

        return options;
    }

    public bool Matches(AsusOptions options) => SelectedDevice.Id == options.DeviceId
        && CoreTemperatureWarningC == options.EffectiveCoreTemperatureWarningC && MemoryTemperatureWarningC == options.EffectiveMemoryTemperatureWarningC
        && Math.Abs(ImbalanceActivationA - options.ImbalanceAmperageThreshold) < 0.0001
        && Math.Abs(ImbalancePercentage - options.ImbalancePercentageThreshold) < 0.0001
        && Math.Abs(ImbalanceDurationMs - options.ImbalanceDurationThresholdMs) < 0.0001
        && Math.Abs((float)OvercurrentA - options.WireOvercurrentAmperageThreshold) < 0.0001
        && Math.Abs(OvercurrentDurationMs - options.WireOvercurrentThresholdMs) < 0.0001;
}
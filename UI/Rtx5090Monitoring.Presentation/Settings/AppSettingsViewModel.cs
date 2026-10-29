using Rtx5090Monitoring.Presentation.Infrastructure;
using Rtx5090Monitoring.Presentation.Telemetry;

namespace Rtx5090Monitoring.Presentation.Settings;

public sealed class AppSettingsViewModel(AppPreferences preferences) : ObservableObject
{
    private AppPreferences _value = preferences;
    public AppPreferences Value => _value;
    public event Action<AppPreferences>? Changed;

    public AppTheme Theme { get => _value.Theme; set => Update(_value with { Theme = value }); }
    public string AccentColor { get => _value.AccentColor; set => Update(_value with { AccentColor = value }); }
    public double TextScale { get => _value.TextScale; set => Update(_value with { TextScale = value }); }
    public bool Fahrenheit { get => _value.Fahrenheit; set => Update(_value with { Fahrenheit = value }); }
    public bool ShowPeakHold { get => _value.ShowPeakHold; set => Update(_value with { ShowPeakHold = value }); }
    public bool ShowHeadroom { get => _value.ShowHeadroom; set => Update(_value with { ShowHeadroom = value }); }
    public bool ShowWireViewPsuCurrent { get => _value.ShowWireViewPsuCurrent; set => Update(_value with { ShowWireViewPsuCurrent = value }); }
    public bool ShowAsusPsuCurrent { get => _value.ShowAsusPsuCurrent; set => Update(_value with { ShowAsusPsuCurrent = value }); }
    public int WireViewPsuConnectorIndex
    {
        get => _value.WireViewPsuConnector - 1;
        set
        {
            if (value is 0 or 1)
            {
                Update(_value with { WireViewPsuConnector = value + 1 });
            }
        }
    }
    public int AsusPsuConnectorIndex
    {
        get => _value.AsusPsuConnector - 1;
        set
        {
            if (value is 0 or 1)
            {
                Update(_value with { AsusPsuConnector = value + 1 });
            }
        }
    }
    public bool ReducedEffects { get => _value.ReducedEffects; set => Update(_value with { ReducedEffects = value }); }
    public bool MinimizeToTray { get => _value.MinimizeToTray; set => Update(_value with { MinimizeToTray = value }); }
    public bool CloseToTray { get => _value.CloseToTray; set => Update(_value with { CloseToTray = value }); }
    public bool StartWithWindows { get => _value.StartWithWindows; set => Update(_value with { StartWithWindows = value }); }
    public bool StartMinimized { get => _value.StartMinimized; set => Update(_value with { StartMinimized = value }); }
    public bool AutomaticallyCheckForUpdates { get => _value.AutomaticallyCheckForUpdates; set => Update(_value with { AutomaticallyCheckForUpdates = value }); }
    public int UpdateFrequencyIndex
    {
        get => (int)_value.UpdateFrequency;
        set
        {
            if (value is >= 0 and <= 2)
            {
                Update(_value with { UpdateFrequency = (Updates.UpdateFrequency)value });
            }
        }
    }

    public bool WarningNotifications { get => _value.WarningNotifications; set => Update(_value with { WarningNotifications = value }); }
    public double CoreTemperatureWarningC { get => _value.CoreTemperatureWarningC; set => Update(_value with { CoreTemperatureWarningC = value }); }
    public double MemoryTemperatureWarningC { get => _value.MemoryTemperatureWarningC; set => Update(_value with { MemoryTemperatureWarningC = value }); }
    public string ServiceAddress { get => _value.ServiceAddress; set => Update(_value with { ServiceAddress = value.TrimEnd('/') + "/" }); }
    public string Message
    {
        get;
        set => Set(ref field, value);
    } = string.Empty;
    public PinMetric PinMetric { get => _value.PinMetric; set => Update(_value with { PinMetric = value }); }

    private bool _gpuStatusTabRequired = true;

    public bool CanChooseGpuStatusTab => !_gpuStatusTabRequired;

    public bool ShowSeparateGpuStatusTab
    {
        get => _gpuStatusTabRequired || _value.ShowSeparateGpuStatusTab;
        set
        {
            if (!_gpuStatusTabRequired)
            {
                Update(_value with { ShowSeparateGpuStatusTab = value });
            }
        }
    }

    public void SetGpuStatusTabRequired(bool required)
    {
        if (Set(ref _gpuStatusTabRequired, required))
        {
            Raise(nameof(CanChooseGpuStatusTab));
            Raise(nameof(ShowSeparateGpuStatusTab));
        }
    }

    private static readonly string[] StartupTabs = ["", "wireview-pro2", "asus-astral", "generic-nvidia", "msi-psu"];

    public int StartupTabIndex
    {
        get => Math.Max(0, Array.IndexOf(StartupTabs, _value.StartupTabId));
        set
        {
            if (value >= 0 && value < StartupTabs.Length)
            {
                Update(_value with { StartupTabId = StartupTabs[value] });
            }
        }
    }

    public double WindowWidth { get => _value.WindowWidth; set => Update(_value with { WindowWidth = value }); }
    public double WindowHeight { get => _value.WindowHeight; set => Update(_value with { WindowHeight = value }); }

    public void SaveWindowSize(double width, double height)
    {
        Update(_value with { WindowWidth = width, WindowHeight = height });
        Raise(nameof(WindowWidth));
        Raise(nameof(WindowHeight));
    }

    private void Update(AppPreferences candidate, [System.Runtime.CompilerServices.CallerMemberName] string? property = null)
    {
        try
        {
            candidate.Validate();
            if (_value == candidate)
            {
                return;
            }

            _value = candidate;
            Raise(property);
            Changed?.Invoke(candidate);
            Message = string.Empty;
        }
        catch (ArgumentException exception)
        {
            Message = exception.Message;
        }
    }
}
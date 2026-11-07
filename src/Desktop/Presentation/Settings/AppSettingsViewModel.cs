using Rtx5090Monitoring.Presentation.Infrastructure;
using Rtx5090Monitoring.Presentation.Telemetry;

namespace Rtx5090Monitoring.Presentation.Settings;

public sealed class AppSettingsViewModel(AppPreferences preferences) : ObservableObject
{
    public AppPreferences Value { get; private set; } = preferences;
    public event Action<AppPreferences>? Changed;

    public AppTheme Theme { get => Value.Theme; set => Update(Value with { Theme = value }); }
    public string AccentColor { get => Value.AccentColor; set => Update(Value with { AccentColor = value }); }
    public double TextScale { get => Value.TextScale; set => Update(Value with { TextScale = value }); }
    public bool Fahrenheit { get => Value.Fahrenheit; set => Update(Value with { Fahrenheit = value }); }
    public bool ShowPeakHold { get => Value.ShowPeakHold; set => Update(Value with { ShowPeakHold = value }); }
    public bool ShowHeadroom { get => Value.ShowHeadroom; set => Update(Value with { ShowHeadroom = value }); }
    public bool ShowWireViewPsuCurrent { get => Value.ShowWireViewPsuCurrent; set => Update(Value with { ShowWireViewPsuCurrent = value }); }
    public bool ShowAsusPsuCurrent { get => Value.ShowAsusPsuCurrent; set => Update(Value with { ShowAsusPsuCurrent = value }); }
    public int WireViewPsuConnectorIndex
    {
        get => Value.WireViewPsuConnector - 1;
        set
        {
            if (value is 0 or 1)
            {
                Update(Value with { WireViewPsuConnector = value + 1 });
            }
        }
    }
    public int AsusPsuConnectorIndex
    {
        get => Value.AsusPsuConnector - 1;
        set
        {
            if (value is 0 or 1)
            {
                Update(Value with { AsusPsuConnector = value + 1 });
            }
        }
    }
    public bool ReducedEffects { get => Value.ReducedEffects; set => Update(Value with { ReducedEffects = value }); }
    public bool MinimizeToTray { get => Value.MinimizeToTray; set => Update(Value with { MinimizeToTray = value }); }
    public bool CloseToTray { get => Value.CloseToTray; set => Update(Value with { CloseToTray = value }); }
    public bool ConfirmBeforeExit
    {
        get => !Value.SuppressExitConfirmation;
        set => Update(Value with { SuppressExitConfirmation = !value });
    }

    public bool StartWithWindows { get => Value.StartWithWindows; set => Update(Value with { StartWithWindows = value }); }
    public bool StartMinimized { get => Value.StartMinimized; set => Update(Value with { StartMinimized = value }); }
    public bool AutomaticallyCheckForUpdates { get => Value.AutomaticallyCheckForUpdates; set => Update(Value with { AutomaticallyCheckForUpdates = value }); }
    public int UpdateFrequencyIndex
    {
        get => (int)Value.UpdateFrequency;
        set
        {
            if (value is >= 0 and <= 2)
            {
                Update(Value with { UpdateFrequency = (Updates.UpdateFrequency)value });
            }
        }
    }

    public bool WarningNotifications { get => Value.WarningNotifications; set => Update(Value with { WarningNotifications = value }); }
    public double CoreTemperatureWarningC { get => Value.CoreTemperatureWarningC; set => Update(Value with { CoreTemperatureWarningC = value }); }
    public double MemoryTemperatureWarningC { get => Value.MemoryTemperatureWarningC; set => Update(Value with { MemoryTemperatureWarningC = value }); }
    public string ServiceAddress { get => Value.ServiceAddress; set => Update(Value with { ServiceAddress = value.TrimEnd('/') + "/" }); }
    public string Message
    {
        get;
        set => Set(ref field, value);
    } = string.Empty;
    public PinMetric PinMetric { get => Value.PinMetric; set => Update(Value with { PinMetric = value }); }

    private bool _gpuStatusTabRequired = true;

    public bool CanChooseGpuStatusTab => !_gpuStatusTabRequired;

    public bool ShowSeparateGpuStatusTab
    {
        get => _gpuStatusTabRequired || Value.ShowSeparateGpuStatusTab;
        set
        {
            if (!_gpuStatusTabRequired)
            {
                Update(Value with { ShowSeparateGpuStatusTab = value });
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

    private static readonly string[] StartupTabs = ["", "wireview-pro2", "asus-astral", "generic-nvidia", "msi-psu", "performance"];

    public int StartupTabIndex
    {
        get => Math.Max(0, Array.IndexOf(StartupTabs, Value.StartupTabId));
        set
        {
            if (value >= 0 && value < StartupTabs.Length)
            {
                Update(Value with { StartupTabId = StartupTabs[value] });
            }
        }
    }

    public double WindowWidth { get => Value.WindowWidth; set => Update(Value with { WindowWidth = value }); }
    public double WindowHeight { get => Value.WindowHeight; set => Update(Value with { WindowHeight = value }); }

    public void SaveWindowSize(double width, double height)
    {
        Update(Value with { WindowWidth = width, WindowHeight = height });
        Raise(nameof(WindowWidth));
        Raise(nameof(WindowHeight));
    }

    private void Update(AppPreferences candidate, [System.Runtime.CompilerServices.CallerMemberName] string? property = null)
    {
        try
        {
            candidate.Validate();
            if (Value == candidate)
            {
                return;
            }

            Value = candidate;
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
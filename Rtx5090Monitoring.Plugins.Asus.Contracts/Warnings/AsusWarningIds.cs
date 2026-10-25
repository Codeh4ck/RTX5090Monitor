namespace Rtx5090Monitoring.Plugins.Asus.Contracts.Warnings;

public static class AsusWarningIds
{
    public const string Overcurrent = "asus-astral/pin-overcurrent";
    public const string Imbalance = "asus-astral/current-imbalance";
    public const string LowVoltage = "asus-astral/low-pin-voltage";
    public const string HighVoltage = "asus-astral/high-pin-voltage";
    public const string CoreTemperature = "asus-astral/core-temperature";
    public const string MemoryTemperature = "asus-astral/memory-temperature";
}

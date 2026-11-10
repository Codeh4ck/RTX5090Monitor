namespace RTXLens.Plugins.WireView.Contracts.Common;

public sealed class PinReading
{
    public double Voltage { get; init; }
    public double Current { get; init; }
    public double Power { get; init; }
}

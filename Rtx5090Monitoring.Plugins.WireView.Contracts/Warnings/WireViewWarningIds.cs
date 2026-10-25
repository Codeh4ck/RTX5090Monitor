namespace Rtx5090Monitoring.Plugins.WireView.Contracts.Warnings;

public static class WireViewWarningIds
{
    public const string Overtemperature = "wireview-pro2/overtemperature";
    public const string Overcurrent = "wireview-pro2/total-overcurrent";
    public const string PinOvercurrent = "wireview-pro2/pin-overcurrent";
    public const string Overpower = "wireview-pro2/overpower";
    public const string Imbalance = "wireview-pro2/current-imbalance";
    public const string UnknownFault = "wireview-pro2/unknown-fault";
}

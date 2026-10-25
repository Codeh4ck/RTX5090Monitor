namespace Rtx5090Monitoring.Contracts.Warnings;

public sealed record WarningObservation(string PluginId, string DeviceId, string TypeId, bool Available, bool Active, string Detail)
{
    public string Id => PluginId + "/" + Uri.EscapeDataString(DeviceId) + "/" + TypeId;
}

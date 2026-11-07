namespace Rtx5090Monitoring.Contracts.Configuration;

public sealed record ActiveSettingsProfile(SettingsProfile Profile, string GpuDeviceId, string? WireViewDeviceId)
{
    public bool AppliesToConnector(string pluginId, string? deviceId) => deviceId is not null && pluginId switch
    {
        "asus-astral" or "generic-nvidia" => deviceId == GpuDeviceId,
        "wireview-pro2" => deviceId == WireViewDeviceId,
        _ => false
    };

    public const string WireViewProfileOvercurrent = "wireview-pro2/profile-pin-overcurrent";
}

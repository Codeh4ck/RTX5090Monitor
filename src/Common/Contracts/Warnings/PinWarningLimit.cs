namespace RTXLens.Contracts.Warnings;

public sealed record PinWarningLimit(string PluginId, string DeviceId, double OvercurrentAmperes);

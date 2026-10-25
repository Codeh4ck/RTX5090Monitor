namespace Rtx5090Monitoring.Contracts.Alerts;

public sealed record SensorAlert(string PluginId, string DeviceId, string Kind, DateTimeOffset ReportedAt,
    AlertPin[] Pins, double? PercentageDifference = null);

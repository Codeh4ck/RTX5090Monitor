namespace Rtx5090Monitoring.Contracts.Health;

public sealed record HealthResponse(string Status, DateTimeOffset StartedAt, double UptimeSeconds, PluginHealth[] Plugins,
    long ConfigurationRevision = 0, string? ConfigurationError = null);

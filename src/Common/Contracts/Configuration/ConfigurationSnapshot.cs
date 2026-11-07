namespace Rtx5090Monitoring.Contracts.Configuration;

public sealed record ConfigurationSnapshot(long Revision, ServiceConfiguration Configuration, string? LastError = null, string? Version = null);

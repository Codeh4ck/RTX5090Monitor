namespace Rtx5090Monitoring.Core.Configuration;

public sealed class ConfigurationConflictException() : Exception("Configuration changed elsewhere. Reload it before applying your changes.");

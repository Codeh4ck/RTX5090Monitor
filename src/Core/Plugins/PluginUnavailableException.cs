namespace Rtx5090Monitoring.Core.Plugins;

public sealed class PluginUnavailableException(string message) : Exception(message);

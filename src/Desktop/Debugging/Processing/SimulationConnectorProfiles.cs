namespace Rtx5090Monitoring.Debugging.Processing;

public static class SimulationConnectorProfiles
{
    public static Core.ConnectorProfileResolver CreateResolver() => new(
        new[] { "generic-nvidia", "asus-astral", "wireview-pro2", "msi-psu" }
            .Select(id => new SimulationConnectorSourceProvider(id)));
}

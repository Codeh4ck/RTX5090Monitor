namespace Rtx5090Monitoring.Service.Endpoints;

internal static class MonitoringEndpoints
{
    public static void MapMonitoringEndpoints(this WebApplication app)
    {
        app.MapHealthEndpoint().WithDescription("Returns the health status of the service and its uptime");
        app.MapVersionEndpoint();
        app.MapSensorsEndpoint().WithDescription("Streams sensor data to the client via WebSocket");
        app.MapConfigurationEndpoints();
        app.MapPluginEndpoints();
        app.MapPost("/restart", (Services.IServiceRestart restart) =>
        {
            restart.RequestRestart();
            return Results.Ok();
        });
    }
}

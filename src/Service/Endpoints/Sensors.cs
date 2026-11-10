using RTXLens.Core.Streaming;

namespace RTXLens.Service.Endpoints;

internal static class Sensors
{
    public static RouteHandlerBuilder MapSensorsEndpoint(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/sensors", StreamAsync);

    private static Task StreamAsync(HttpContext context, SensorStreamHub hub, IHostApplicationLifetime lifetime) =>
        SensorWebSocket.StreamAsync(context, hub, lifetime);
}

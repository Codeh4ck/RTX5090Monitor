using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using RTXLens.Core.Plugins;
using RTXLens.Core.Streaming;
using RTXLens.Plugins.WireView.Services;
using RTXLens.Plugins.WireView.Streaming;
using RTXLens.Plugins.WireView.Serialization;

namespace RTXLens.Plugins.WireView.Endpoints;

internal static class WireViewSensorEndpoints
{
    public static void MapWireViewSensorEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/sensors", async (PluginRuntimeManager runtime, CancellationToken token) =>
        {
            var snapshot = await WireViewAccess.ExecuteAsync(runtime, plugin => plugin.Latest!, token);
            return Results.Json(snapshot, WireViewJsonContext.Default.SensorResponse);
        });

        group.MapGet("/ws", async (HttpContext context, PluginRuntimeManager runtime, WireViewStream stream, IHostApplicationLifetime lifetime) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            await WireViewAccess.ExecuteAsync(runtime, _ => true, context.RequestAborted);
            await SensorWebSocket.StreamAsync(context, stream.Hub, lifetime);
        });
    }
}

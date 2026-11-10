using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using RTXLens.Core.Plugins;
using RTXLens.Core.Streaming;
using RTXLens.Plugins.Asus.Serialization;
using RTXLens.Plugins.Asus.Services;
using RTXLens.Plugins.Asus.Streaming;

namespace RTXLens.Plugins.Asus.Endpoints;

internal static class AsusSensorEndpoints
{
    public static void MapAsusSensorEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/sensors", async (PluginRuntimeManager runtime, CancellationToken token) =>
        {
            var snapshot = await AsusAccess.ExecuteAsync(runtime, plugin => plugin.Latest!, token);
            return Results.Json(snapshot, AsusJsonContext.Default.GpuSensorSnapshot);
        });

        group.MapGet("/devices", async (PluginRuntimeManager runtime, CancellationToken token) =>
        {
            var devices = await runtime.ExecuteAsync(AsusPlugin.PluginId, plugin => ((AsusPlugin)plugin).Devices.ToArray(), token);
            return Results.Json(devices, AsusJsonContext.Default.AsusDeviceInfoArray);
        });

        group.MapGet("/ws", async (HttpContext context, PluginRuntimeManager runtime, AsusStream stream, IHostApplicationLifetime lifetime) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            await AsusAccess.ExecuteAsync(runtime, _ => true, context.RequestAborted);
            await SensorWebSocket.StreamAsync(context, stream.Hub, lifetime);
        });
    }
}

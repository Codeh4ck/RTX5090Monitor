using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Core;
using Rtx5090Monitoring.Core.Http;
using Rtx5090Monitoring.Core.Plugins;
using Rtx5090Monitoring.Plugins.WireView.Contracts.Output;
using Rtx5090Monitoring.Plugins.WireView.Serialization;

namespace Rtx5090Monitoring.Plugins.WireView.Endpoints;

public static class WireViewEndpoints
{
    public static void MapWireViewEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/plugins/wireview-pro2")
            .AddEndpointFilter<ApiExceptionFilter>();

        group.MapGet("/health", (SensorStateStore store, TimeProvider clock) =>
        {
            var health = store.Snapshot(clock.GetUtcNow(), []).Plugins.Single(plugin => plugin.PluginId == WireViewPlugin.PluginId);
            var status = health.State switch
            {
                PluginState.Ready => "ok",
                PluginState.Disabled => "disabled",
                _ => "no_device"
            };

            return Results.Json(new HealthResponse { Status = status }, WireViewJsonContext.Default.HealthResponse);
        });

        group.MapWireViewConfigEndpoints();
        group.MapWireViewSensorEndpoints();
        group.MapWireViewDeviceEndpoints();

        group.MapPost("/restart", async (PluginRuntimeManager runtime, CancellationToken token) =>
        {
            await runtime.RestartAsync(WireViewPlugin.PluginId, token);
            return Results.Ok();
        });
    }
}
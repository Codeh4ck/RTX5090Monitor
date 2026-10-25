using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Core;
using Rtx5090Monitoring.Core.Http;
using Rtx5090Monitoring.Core.Plugins;
using Rtx5090Monitoring.Plugins.Asus.Contracts.Output;
using Rtx5090Monitoring.Plugins.Asus.Serialization;

namespace Rtx5090Monitoring.Plugins.Asus.Endpoints;

public static class AsusEndpoints
{
    public static void MapAsusEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/plugins/asus-astral").AddEndpointFilter<ApiExceptionFilter>();
        group.MapGet("/health", (SensorStateStore store, TimeProvider clock) =>
        {
            var health = store.Snapshot(clock.GetUtcNow(), []).Plugins.Single(plugin => plugin.PluginId == AsusPlugin.PluginId);
            var status = health.State switch
            {
                PluginState.Ready => "ok",
                PluginState.Disabled => "disabled",
                PluginState.Error => "error",
                PluginState.Stale => "stale",
                _ => "no_device"
            };

            return Results.Json(new HealthResponse(status), AsusJsonContext.Default.HealthResponse);
        });

        group.MapAsusConfigEndpoints();
        group.MapAsusSensorEndpoints();

        group.MapPost("/restart", async (PluginRuntimeManager runtime, CancellationToken token) =>
        {
            await runtime.RestartAsync(AsusPlugin.PluginId, token);
            return Results.Ok();
        });
    }
}
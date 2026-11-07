using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Rtx5090Monitoring.Core.Http;
using Rtx5090Monitoring.Core.Plugins;
using Rtx5090Monitoring.Plugins.Msi.Serialization;

namespace Rtx5090Monitoring.Plugins.Msi.Endpoints;

public static class MsiEndpoints
{
    public static void MapMsiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/plugins/msi-psu").AddEndpointFilter<ApiExceptionFilter>();
        group.MapPost("/restart", async (PluginRuntimeManager runtime, CancellationToken token) =>
        {
            await runtime.RestartAsync(MsiPlugin.PluginId, token);
            return Results.Ok();
        });

        group.MapGet("/device", async (PluginRuntimeManager runtime, CancellationToken token) =>
        {
            var snapshot = await runtime.ExecuteAsync(MsiPlugin.PluginId, plugin =>
                ((MsiPlugin)plugin).Latest ?? throw new PluginUnavailableException("No current MSI PSU snapshot is available."), token);
            return Results.Json(snapshot, MsiJsonContext.Default.MsiSnapshot);
        });

        group.MapGet("/devices", async (PluginRuntimeManager runtime, CancellationToken token) =>
        {
            var devices = await runtime.ExecuteAsync(MsiPlugin.PluginId, plugin => ((MsiPlugin)plugin).Devices, token);
            return Results.Json(devices, MsiJsonContext.Default.MsiDeviceInfoArray);
        });
    }
}
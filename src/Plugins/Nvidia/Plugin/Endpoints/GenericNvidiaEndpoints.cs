using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Rtx5090Monitoring.Core.Configuration;
using Rtx5090Monitoring.Core.Http;
using Rtx5090Monitoring.Core.Plugins;
using Rtx5090Monitoring.Hardware.Nvidia;
using Rtx5090Monitoring.Plugins.GenericNvidia.Contracts.Devices;
using Rtx5090Monitoring.Plugins.GenericNvidia.Serialization;

namespace Rtx5090Monitoring.Plugins.GenericNvidia.Endpoints;

public static class GenericNvidiaEndpoints
{
    public static void MapGenericNvidiaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/plugins/generic-nvidia").AddEndpointFilter<ApiExceptionFilter>();

        group.MapGet("/devices", async (NvidiaTelemetryReader reader, CancellationToken token) =>
        {
            var devices = await Task.Run(() => reader.GetDevices(refresh: true), token);
            return Results.Json(devices.Select(device => new NvidiaDeviceInfo(device.Id, device.Name, device.LegacyId)).ToArray(),
                GenericNvidiaJsonContext.Default.NvidiaDeviceInfoArray);
        });

        group.MapGet("/config", (IRuntimeConfiguration configuration) => Results.Json(
            configuration.Current.Configuration.Plugins[GenericNvidiaPlugin.PluginId], GenericNvidiaJsonContext.Default.PluginSettings));

        group.MapPost("/restart", async (PluginRuntimeManager runtime, CancellationToken token) =>
        {
            await runtime.RestartAsync(GenericNvidiaPlugin.PluginId, token);
            return Results.Ok();
        });
    }
}

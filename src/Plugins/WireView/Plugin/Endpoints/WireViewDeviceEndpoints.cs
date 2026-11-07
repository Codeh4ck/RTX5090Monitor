using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Rtx5090Monitoring.Core.Plugins;
using Rtx5090Monitoring.Plugins.WireView.Contracts.Input;
using Rtx5090Monitoring.Plugins.WireView.Contracts.Output;
using Rtx5090Monitoring.Plugins.WireView.Services;
using Rtx5090Monitoring.Plugins.WireView.Serialization;

namespace Rtx5090Monitoring.Plugins.WireView.Endpoints;

internal static class WireViewDeviceEndpoints
{
    public static void MapWireViewDeviceEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/device/config", async (PluginRuntimeManager runtime, CancellationToken token) =>
        {
            var configuration = await WireViewAccess.ExecuteAsync(runtime, plugin => plugin.Reader.ReadConfig(), token);

            return configuration is not null
                ? Results.Json(configuration, WireViewJsonContext.Default.DeviceConfig)
                : throw new IOException("Failed to read WireView device configuration.");
        });

        group.MapGet("/device/config/raw", async (PluginRuntimeManager runtime, CancellationToken token) =>
        {
            var bytes = await WireViewAccess.ExecuteAsync(runtime, plugin => plugin.Reader.ReadConfigRaw(), token);

            return bytes is not null
                ? Results.Json(new RawConfigResponse(BitConverter.ToString(bytes), bytes.Length), WireViewJsonContext.Default.RawConfigResponse)
                : throw new IOException("Failed to read raw WireView device configuration.");
        });

        group.MapPost("/faults/clear", async (ClearFaultsRequest request, PluginRuntimeManager runtime, CancellationToken token) =>
        {
            var statusMask = (ushort)~request.FaultStatusMask;
            var logMask = (ushort)~request.FaultLogMask;
            var sent = await WireViewAccess.ExecuteAsync(runtime, plugin => plugin.Reader.ClearFaults(statusMask, logMask), token);

            return sent ? Results.Ok() : throw new IOException("Failed to send the WireView fault-clear command.");
        });
    }
}
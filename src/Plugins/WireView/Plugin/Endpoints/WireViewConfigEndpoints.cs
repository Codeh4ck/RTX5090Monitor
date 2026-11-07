using Rtx5090Monitoring.Core.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Rtx5090Monitoring.Plugins.WireView.Contracts.Input;
using Rtx5090Monitoring.Plugins.WireView.Contracts.Output;
using Rtx5090Monitoring.Plugins.WireView.Serialization;

namespace Rtx5090Monitoring.Plugins.WireView.Endpoints;

internal static class WireViewConfigEndpoints
{
    public static void MapWireViewConfigEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/config", (IRuntimeConfiguration configuration) =>
        {
            var settings = configuration.Current.Configuration.Plugins[WireViewPlugin.PluginId];
            return Results.Json(new ConfigResponse { PollIntervalMs = settings.PollIntervalMs }, WireViewJsonContext.Default.ConfigResponse);
        });

        group.MapPost("/config", async (ConfigRequest request, IRuntimeConfiguration configuration, CancellationToken token) =>
        {
            var updated = await configuration.UpdateAsync(current =>
            {
                var previous = current.Plugins[WireViewPlugin.PluginId];
                var interval = Math.Clamp(request.PollIntervalMs, 100, 30000);
                current.Plugins[WireViewPlugin.PluginId] = previous with
                {
                    PollIntervalMs = interval,
                    StaleAfterMs = Math.Max(previous.StaleAfterMs, interval)
                };
                return current;
            }, token);

            var settings = updated.Configuration.Plugins[WireViewPlugin.PluginId];
            return Results.Json(new ConfigResponse { PollIntervalMs = settings.PollIntervalMs }, WireViewJsonContext.Default.ConfigResponse);
        });
    }
}

using RTXLens.Core.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RTXLens.Plugins.Asus.Contracts.Input;
using RTXLens.Plugins.Asus.Mapping;
using RTXLens.Plugins.Asus.Serialization;

namespace RTXLens.Plugins.Asus.Endpoints;

internal static class AsusConfigEndpoints
{
    public static void MapAsusConfigEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/config", (IRuntimeConfiguration configuration) =>
            Results.Json(AsusConfigMapper.ToResponse(configuration.Current.Configuration.Plugins[AsusPlugin.PluginId]),
                AsusJsonContext.Default.ConfigResponse));

        group.MapPut("/config", async (ConfigRequest request, IRuntimeConfiguration configuration, CancellationToken token) =>
        {
            var updated = await configuration.UpdateAsync(current =>
            {
                current.Plugins[AsusPlugin.PluginId] = AsusConfigMapper.Apply(request, current.Plugins[AsusPlugin.PluginId]);
                return current;
            }, token);

            return Results.Json(AsusConfigMapper.ToResponse(updated.Configuration.Plugins[AsusPlugin.PluginId]), AsusJsonContext.Default.ConfigResponse);
        });
    }
}
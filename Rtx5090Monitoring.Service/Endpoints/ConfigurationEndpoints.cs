using Rtx5090Monitoring.Core.Configuration;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Core.Http;

namespace Rtx5090Monitoring.Service.Endpoints;

internal static class ConfigurationEndpoints
{
    public static void MapConfigurationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/configuration", (HttpContext context, IRuntimeConfiguration configuration) => ConfigurationPrecondition.Respond(context, configuration.Current));

        endpoints.MapPut("/configuration",
            async (ServiceConfiguration settings, HttpContext context, IRuntimeConfiguration configuration, CancellationToken token) =>
            {
                var updated = await configuration.UpdateAsync(_ => settings, token, ConfigurationPrecondition.Read(context.Request));
                return ConfigurationPrecondition.Respond(context, updated);
            }).AddEndpointFilter<ApiExceptionFilter>();

        endpoints.MapGet("/plugins/{pluginId}/settings", (string pluginId, IRuntimeConfiguration configuration) =>
            Results.Ok(configuration.Current.Configuration.Plugins[pluginId])).AddEndpointFilter<ApiExceptionFilter>();

        endpoints.MapPut("/plugins/{pluginId}/settings", UpdatePluginSettingsAsync).AddEndpointFilter<ApiExceptionFilter>();
    }

    private static async Task<IResult> UpdatePluginSettingsAsync(string pluginId, PluginSettings settings, HttpContext context,
        IRuntimeConfiguration configuration, CancellationToken cancellationToken)
    {
        var updated = await configuration.UpdateAsync(current =>
        {
            if (!current.Plugins.ContainsKey(pluginId))
            {
                throw new KeyNotFoundException($"Plugin '{pluginId}' is not installed.");
            }

            current.Plugins[pluginId] = settings;

            return current;
        }, cancellationToken, ConfigurationPrecondition.Read(context.Request));

        return ConfigurationPrecondition.Respond(context, updated);
    }
}

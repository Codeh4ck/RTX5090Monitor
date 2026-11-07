using System.Net;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Core.Configuration;
using Rtx5090Monitoring.Core.Http;
using Rtx5090Monitoring.Service.Configuration;
using Rtx5090Monitoring.Service.Services;

namespace Rtx5090Monitoring.Service.Endpoints;

internal static class WarningProfileEndpoints
{
    public static void MapWarningProfileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var profiles = endpoints.MapGroup("/warning-profiles");

        profiles.MapGet("", (DeviceWarningProfileStore store, SettingsProfileResolver settings, IRuntimeConfiguration config) =>
            Results.Json(store.Catalog(settings.Automatic, config.Current.Configuration), AppJsonContext.Default.DeviceWarningProfileCatalog));

        profiles.MapPut("", (DeviceWarningProfile profile, HttpContext context, DeviceWarningProfileStore store) =>
        {
            if (!IsLocalClient(context.Request))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            store.Save(profile);
            return Results.NoContent();
        }).AddEndpointFilter<ApiExceptionFilter>();

        profiles.MapDelete("/{id}", (string id, HttpContext context, DeviceWarningProfileStore store) =>
        {
            if (!IsLocalClient(context.Request))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            store.Delete(id);
            return Results.NoContent();
        }).AddEndpointFilter<ApiExceptionFilter>();

        endpoints.MapGet("/warning-shutdown", (HttpContext context, WarningShutdownCoordinator shutdown) =>
        {
            if (!IsLocalClient(context.Request))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var reason = shutdown.ReadAfterBoot();
            return reason is null ? Results.NoContent() : Results.Json(reason, AppJsonContext.Default.WarningShutdownReason);
        });

        endpoints.MapDelete("/warning-shutdown/{id}", (string id, HttpContext context, WarningShutdownCoordinator shutdown) =>
        {
            if (!IsLocalClient(context.Request))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            shutdown.Acknowledge(id);
            return Results.NoContent();
        }).AddEndpointFilter<ApiExceptionFilter>();
    }

    private static bool IsLocalClient(HttpRequest request)
    {
        var host = request.Host.Host.Trim('[', ']');
        return !request.Headers.ContainsKey("Origin") && !request.Headers.ContainsKey("Sec-Fetch-Site")
            && (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                || IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address));
    }
}

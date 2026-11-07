using Rtx5090Monitoring.Core.Configuration;
using Rtx5090Monitoring.Contracts.Health;
using Rtx5090Monitoring.Core;
using Rtx5090Monitoring.Service.Utilities;

namespace Rtx5090Monitoring.Service.Endpoints;

internal static class Health
{
    public static RouteHandlerBuilder MapHealthEndpoint(this IEndpointRouteBuilder app) =>
        app.MapGet("/health", (SensorStateStore store, MonitoringServiceLifetime lifetime, TimeProvider clock, IRuntimeConfiguration configuration) =>
        {
            var now = clock.GetUtcNow();
            var current = configuration.Current;

            return Results.Json(new HealthResponse("running", lifetime.StartedAt,
                    Math.Max(0, (now - lifetime.StartedAt).TotalSeconds), store.Snapshot(now, []).Plugins,
                    current.Revision, current.LastError),
                AppJsonContext.Default.HealthResponse);
        });
}

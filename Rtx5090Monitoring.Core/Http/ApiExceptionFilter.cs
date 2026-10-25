using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Rtx5090Monitoring.Contracts.Errors;
using Rtx5090Monitoring.Core.Plugins;
using Rtx5090Monitoring.Core.Configuration;

namespace Rtx5090Monitoring.Core.Http;

public sealed class ApiExceptionFilter(ILogger<ApiExceptionFilter> logger) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (Exception exception) when (!context.HttpContext.Response.HasStarted)
        {
            if (exception is OperationCanceledException && context.HttpContext.RequestAborted.IsCancellationRequested)
            {
                return Results.StatusCode(499);
            }

            var status = exception switch
            {
                PluginUnavailableException => StatusCodes.Status503ServiceUnavailable,
                ConfigurationConflictException => StatusCodes.Status412PreconditionFailed,
                KeyNotFoundException => StatusCodes.Status404NotFound,
                ArgumentException or JsonException => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status500InternalServerError
            };

            if (status == StatusCodes.Status500InternalServerError)
            {
                logger.LogError(exception, "Request failed for {Path}", context.HttpContext.Request.Path);
            }

            var message = status == StatusCodes.Status500InternalServerError
                ? "The operation failed; see service logs." : exception.Message;

            return Results.Json(new ErrorResponse(message), HttpJsonContext.Default.ErrorResponse, statusCode: status);
        }
    }
}

using Microsoft.AspNetCore.Http;
using Rtx5090Monitoring.Contracts.Configuration;

namespace Rtx5090Monitoring.Core.Http;

public static class ConfigurationPrecondition
{
    public static string? Read(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("If-Match", out var value))
        {
            return null;
        }

        var text = value.ToString();
        if (text.Length < 3 || text[0] != '"' || text[^1] != '"' || text.Contains(','))
        {
            throw new ArgumentException("If-Match must contain one quoted configuration version.");
        }

        return text[1..^1];
    }

    public static IResult Respond(HttpContext context, ConfigurationSnapshot snapshot)
    {
        context.Response.Headers.ETag = $"\"{snapshot.Version}\"";
        return Results.Ok(snapshot);
    }
}

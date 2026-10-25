using System.Net;

namespace Rtx5090Monitoring.Client.Http;

public sealed class MonitoringApiException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public bool IsConflict => StatusCode == HttpStatusCode.PreconditionFailed;
}

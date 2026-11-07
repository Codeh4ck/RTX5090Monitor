using System.Net;

namespace Rtx5090Monitoring.UI.Tests.TestDoubles;

internal sealed class VersionResponseHandler(HttpStatusCode status, string json = "") : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json) });
}

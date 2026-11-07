using System.Net;
using System.Text;

namespace Rtx5090Monitoring.UI.Tests.TestDoubles;

internal sealed class JsonResponseHandler(string json) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
}

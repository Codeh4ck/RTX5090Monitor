using Rtx5090Monitoring.Service.Services;

namespace Rtx5090Monitoring.Tests.TestDoubles;

internal sealed class TestServiceRestart : IServiceRestart
{
    public int RequestCount { get; private set; }
    public void RequestRestart() => RequestCount++;
}

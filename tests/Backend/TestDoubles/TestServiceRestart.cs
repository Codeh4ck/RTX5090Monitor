using RTXLens.Service.Services;

namespace RTXLens.Tests.TestDoubles;

internal sealed class TestServiceRestart : IServiceRestart
{
    public int RequestCount { get; private set; }
    public void RequestRestart() => RequestCount++;
}

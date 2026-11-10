using RTXLens.Core.Streaming;

namespace RTXLens.Plugins.WireView.Streaming;

public sealed class WireViewStream : IDisposable
{
    public SensorStreamHub Hub { get; } = new();
    public void Dispose() => Hub.Dispose();
}

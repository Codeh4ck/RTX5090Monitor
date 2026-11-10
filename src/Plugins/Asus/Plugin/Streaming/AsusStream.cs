using RTXLens.Core.Streaming;

namespace RTXLens.Plugins.Asus.Streaming;

public sealed class AsusStream : IDisposable
{
    public SensorStreamHub Hub { get; } = new();

    public void Dispose() => Hub.Dispose();
}

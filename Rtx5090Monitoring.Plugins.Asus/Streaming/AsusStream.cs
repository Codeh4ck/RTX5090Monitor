using Rtx5090Monitoring.Core.Streaming;

namespace Rtx5090Monitoring.Plugins.Asus.Streaming;

public sealed class AsusStream : IDisposable
{
    public SensorStreamHub Hub { get; } = new();

    public void Dispose() => Hub.Dispose();
}

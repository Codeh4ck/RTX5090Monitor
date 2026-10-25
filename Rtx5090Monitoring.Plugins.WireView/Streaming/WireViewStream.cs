using Rtx5090Monitoring.Core.Streaming;

namespace Rtx5090Monitoring.Plugins.WireView.Streaming;

public sealed class WireViewStream : IDisposable
{
    public SensorStreamHub Hub { get; } = new();
    public void Dispose() => Hub.Dispose();
}

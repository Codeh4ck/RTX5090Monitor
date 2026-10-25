using System.Threading.Channels;

namespace Rtx5090Monitoring.Core.Streaming;

public sealed class Subscription : IDisposable
{
    private readonly SensorStreamHub _owner;
    private readonly Channel<byte[]> _channel;

    internal Subscription(SensorStreamHub owner, Channel<byte[]> channel)
    {
        _owner = owner;
        _channel = channel;
    }

    public ChannelReader<byte[]> Reader => _channel.Reader;

    public void Dispose() => _owner.Remove(_channel);
}
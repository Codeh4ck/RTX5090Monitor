using System.Threading.Channels;

namespace Rtx5090Monitoring.Core.Utilities;

public sealed class AsyncSignal
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropOldest
    });

    public void Set() => _channel.Writer.TryWrite(true);

    public async Task WaitAsync(int milliseconds, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(milliseconds);

        try
        {
            await _channel.Reader.ReadAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // A normal scheduling delay elapsed without an update.
        }
    }
}

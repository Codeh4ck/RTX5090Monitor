using System.Net.WebSockets;

namespace Rtx5090Monitoring.UI.Tests.TestDoubles;

internal sealed class FakeWebSocket(IEnumerable<SocketPacket> packets) : WebSocket
{
    private readonly Queue<SocketPacket> _packets = new(packets);
    private WebSocketState _state = WebSocketState.Open;
    public bool CloseAcknowledged { get; private set; }
    public override WebSocketCloseStatus? CloseStatus => null;
    public override string? CloseStatusDescription => null;
    public override string? SubProtocol => null;
    public override WebSocketState State => _state;
    public override void Abort() => _state = WebSocketState.Aborted;
    public override void Dispose() => _state = WebSocketState.Closed;
    public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
    {
        _state = WebSocketState.Closed;
        return Task.CompletedTask;
    }
    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
    {
        CloseAcknowledged = true;
        return Task.CompletedTask;
    }
    public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var packet = _packets.Count > 0 ? _packets.Dequeue() : new SocketPacket([], WebSocketMessageType.Close);
        packet.Bytes.AsSpan().CopyTo(buffer.AsSpan());
        return Task.FromResult(new WebSocketReceiveResult(packet.Bytes.Length, packet.Type, packet.EndOfMessage));
    }
    public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
